-- ============================================================
-- Migração 002: Fase 2 — Embarques, Chegada e Conferência Administrativa
-- ------------------------------------------------------------
-- Adiciona: tipo_negocio, comissão paga/não paga e numeração de
-- embarque em negociacoes; tabelas negociacao_produtores (desmembramento
-- por produtor), embarques, embarque_itens (chegada embutida por
-- categoria) e embarque_conferencias. Migra o status das negociações
-- Fechado existentes para o novo ciclo de 4 estados (EmNegociacao,
-- Fechado, EmEntrega, Concluido) e gera um lote + embarque sintéticos
-- ("Emb. 0") por negociação para preservar o histórico de entrega já
-- registrado na Fase 1 (qtd_entregue), já que esse valor passa a ser
-- recalculado a partir de embarque_itens.qtd_chegou.
--
-- Seguro de rodar mais de uma vez (idempotente).
-- Rodar primeiro contra uma cópia do banco antes de aplicar em produção.
-- ============================================================

BEGIN;

-- 1) Colunas novas em negociacoes
ALTER TABLE negociacoes
    ADD COLUMN IF NOT EXISTS tipo_negocio            VARCHAR(10) NOT NULL DEFAULT 'KG',
    ADD COLUMN IF NOT EXISTS comissao_paga            BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS comissao_paga_em         TIMESTAMP DEFAULT NULL,
    ADD COLUMN IF NOT EXISTS comissao_paga_por        INTEGER REFERENCES usuarios(id),
    ADD COLUMN IF NOT EXISTS embarques_ultimo_numero  INTEGER NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_negociacoes_tipo_negocio') THEN
        ALTER TABLE negociacoes ADD CONSTRAINT chk_negociacoes_tipo_negocio CHECK (tipo_negocio IN ('Perna','KG'));
    END IF;
END $$;

-- 2) Desmembramento por produtor/lote (um lote = um produtor + uma categoria)
CREATE TABLE IF NOT EXISTS negociacao_produtores (
    id               SERIAL PRIMARY KEY,
    negociacao_id    INTEGER NOT NULL REFERENCES negociacoes(id) ON DELETE CASCADE,
    categoria_id     INTEGER NOT NULL REFERENCES categorias(id),
    produtor_origem  VARCHAR(150) NOT NULL,
    qtd_cb           INTEGER NOT NULL CHECK (qtd_cb > 0),
    observacoes      VARCHAR(500),
    criado_em        TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em    TIMESTAMP DEFAULT NULL
);
CREATE INDEX IF NOT EXISTS idx_neg_produtores_negociacao ON negociacao_produtores(negociacao_id);
CREATE INDEX IF NOT EXISTS idx_neg_produtores_neg_cat ON negociacao_produtores(negociacao_id, categoria_id);

-- Evita lote duplicado pro mesmo produtor+categoria (ex.: dois embarques criados ao
-- mesmo tempo, ambos tentando criar o lote na hora porque nenhum existia ainda).
-- Normaliza só espaço/caixa (não acentuação) — "Jose"/"jose " colidem, "José"/"Jose" não.
CREATE UNIQUE INDEX IF NOT EXISTS uq_neg_produtores_nome_categoria
    ON negociacao_produtores (negociacao_id, categoria_id, LOWER(TRIM(produtor_origem)));

-- 3) Embarques (carregamento de caminhão, vinculado à negociação e ao lote/produtor)
CREATE TABLE IF NOT EXISTS embarques (
    id                       SERIAL PRIMARY KEY,
    negociacao_id            INTEGER NOT NULL REFERENCES negociacoes(id) ON DELETE CASCADE,
    numero                   INTEGER NOT NULL,             -- sequencial dentro da negociação ("Emb. 1", "Emb. 2"...)
    produtor_origem          VARCHAR(150) NOT NULL,        -- snapshot do produtor do embarque
    municipio_destino_id     INTEGER REFERENCES municipios_destino(id),
    data_embarque            DATE DEFAULT NULL,
    nf                       VARCHAR(30) DEFAULT NULL,
    gta                      VARCHAR(30) DEFAULT NULL,
    observacoes_chegada      VARCHAR(500) DEFAULT NULL,
    chegada_confirmada_em    TIMESTAMP DEFAULT NULL,
    chegada_confirmada_por   INTEGER REFERENCES usuarios(id),
    criado_em                TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    criado_por               INTEGER NOT NULL REFERENCES usuarios(id),
    atualizado_em            TIMESTAMP DEFAULT NULL,
    UNIQUE (negociacao_id, numero)
);
CREATE INDEX IF NOT EXISTS idx_embarques_negociacao ON embarques(negociacao_id);

-- 4) Itens do embarque, por categoria — chegada embutida aqui (qtd_chegou,
--    peso_medio_entrada, animais_debilitados), sem tabela própria: é relação
--    1:1 estrita (um evento de chegada por embarque), mesmo espírito de
--    qtd_entregue/status_entrega embutidos em negociacao_itens na Fase 1.
CREATE TABLE IF NOT EXISTS embarque_itens (
    id                     SERIAL PRIMARY KEY,
    embarque_id            INTEGER NOT NULL REFERENCES embarques(id) ON DELETE CASCADE,
    negociacao_produtor_id INTEGER NOT NULL REFERENCES negociacao_produtores(id) ON DELETE CASCADE,
    qtd_embarcada          INTEGER NOT NULL CHECK (qtd_embarcada > 0),
    qtd_chegou             INTEGER CHECK (qtd_chegou IS NULL OR qtd_chegou >= 0),
    peso_medio_entrada     DECIMAL(10,2) DEFAULT NULL,
    animais_debilitados    INTEGER NOT NULL DEFAULT 0 CHECK (animais_debilitados >= 0),
    UNIQUE (embarque_id, negociacao_produtor_id)
);
CREATE INDEX IF NOT EXISTS idx_embarque_itens_embarque ON embarque_itens(embarque_id);
CREATE INDEX IF NOT EXISTS idx_embarque_itens_lote ON embarque_itens(negociacao_produtor_id);

-- A exclusão de um lote individual (com embarques vinculados) já é bloqueada em
-- código (ProdutorService.Excluir, via ContarEmbarquesVinculados) — o RESTRICT
-- original aqui era redundante e quebrava o cascade legítimo de excluir a
-- negociação inteira (negociacoes -> negociacao_produtores é CASCADE, mas
-- negociacao_produtores -> embarque_itens ficava RESTRICT, travando o meio do
-- caminho). Corrige para CASCADE mesmo em bancos onde a tabela já foi criada
-- com a constraint antiga.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'embarque_itens_negociacao_produtor_id_fkey' AND confdeltype = 'r'
    ) THEN
        ALTER TABLE embarque_itens DROP CONSTRAINT embarque_itens_negociacao_produtor_id_fkey;
        ALTER TABLE embarque_itens ADD CONSTRAINT embarque_itens_negociacao_produtor_id_fkey
            FOREIGN KEY (negociacao_produtor_id) REFERENCES negociacao_produtores(id) ON DELETE CASCADE;
    END IF;
END $$;

-- 5) Conferência administrativa, 1:1 com embarque
CREATE TABLE IF NOT EXISTS embarque_conferencias (
    id                        SERIAL PRIMARY KEY,
    embarque_id               INTEGER NOT NULL UNIQUE REFERENCES embarques(id) ON DELETE CASCADE,
    status                    VARCHAR(20) NOT NULL DEFAULT 'EmAndamento', -- EmAndamento | Finalizada
    valor_total_negociacao    DECIMAL(12,2) DEFAULT NULL,
    valor_total_icms          DECIMAL(12,2) DEFAULT NULL,
    comissao_cb               DECIMAL(10,2) DEFAULT NULL,
    icms_cb                   DECIMAL(10,2) DEFAULT NULL,
    frete_cb                  DECIMAL(10,2) DEFAULT NULL,
    despesa_cb                DECIMAL(10,2) DEFAULT NULL,
    rs_cb                     DECIMAL(10,2) DEFAULT NULL,
    total_final_cb            DECIMAL(10,2) DEFAULT NULL,
    rs_kg_negociacao          DECIMAL(10,4) DEFAULT NULL,
    rs_kg_colocado            DECIMAL(10,4) DEFAULT NULL,
    percentual_quebra_desvio  DECIMAL(6,2) DEFAULT NULL,
    observacao_ocorrencias    VARCHAR(500) DEFAULT NULL,
    finalizada_em             TIMESTAMP DEFAULT NULL,
    finalizada_por            INTEGER REFERENCES usuarios(id),
    criado_em                 TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em             TIMESTAMP DEFAULT NULL
);

-- 6) Backfill de status: negociações Fechado migram conforme entrega já
--    registrada na Fase 1 (idempotente — só reafeta linhas ainda 'Fechado').
WITH agregado AS (
    SELECT n.id,
           COALESCE(SUM(ni.qtd_negociada), 0) AS qtd_neg,
           COALESCE(SUM(ni.qtd_entregue), 0)  AS qtd_ent
    FROM negociacoes n
    JOIN negociacao_itens ni ON ni.negociacao_id = n.id
    WHERE n.status = 'Fechado'
    GROUP BY n.id
)
UPDATE negociacoes n SET status = CASE
        WHEN a.qtd_ent = 0 THEN 'Fechado'
        WHEN a.qtd_neg > 0 AND a.qtd_ent >= a.qtd_neg THEN 'Concluido'
        ELSE 'EmEntrega'
    END
FROM agregado a
WHERE n.id = a.id AND n.status = 'Fechado';

-- 7) Backfill sintético: cria um lote + embarque "Emb. 0" por negociação
--    reproduzindo a quantidade já entregue na Fase 1, para que qtd_entregue
--    recalculado a partir de embarque_itens.qtd_chegou não perca esse histórico.
INSERT INTO negociacao_produtores (negociacao_id, categoria_id, produtor_origem, qtd_cb, observacoes, criado_em)
SELECT ni.negociacao_id, ni.categoria_id, '(Fase 1 — histórico)', ni.qtd_entregue,
       'Gerado automaticamente na migração 002 para preservar o saldo já recebido na Fase 1.',
       CURRENT_TIMESTAMP
FROM negociacao_itens ni
WHERE ni.qtd_entregue > 0
  AND NOT EXISTS (
        SELECT 1 FROM negociacao_produtores np
        WHERE np.negociacao_id = ni.negociacao_id AND np.categoria_id = ni.categoria_id
          AND np.produtor_origem = '(Fase 1 — histórico)'
      );

INSERT INTO embarques (negociacao_id, numero, produtor_origem, municipio_destino_id, data_embarque, chegada_confirmada_em, criado_em, criado_por)
SELECT DISTINCT n.id, 0, '(Fase 1 — histórico)', n.municipio_destino_id, n.data_fechamento::date, n.data_fechamento,
       CURRENT_TIMESTAMP, (SELECT id FROM usuarios WHERE perfil = 'Admin' ORDER BY id LIMIT 1)
FROM negociacoes n
JOIN negociacao_produtores np ON np.negociacao_id = n.id AND np.produtor_origem = '(Fase 1 — histórico)'
WHERE NOT EXISTS (SELECT 1 FROM embarques e WHERE e.negociacao_id = n.id AND e.numero = 0);

INSERT INTO embarque_itens (embarque_id, negociacao_produtor_id, qtd_embarcada, qtd_chegou, peso_medio_entrada, animais_debilitados)
SELECT e.id, np.id, np.qtd_cb, np.qtd_cb, ni.peso_medio, 0
FROM negociacao_produtores np
JOIN embarques e ON e.negociacao_id = np.negociacao_id AND e.numero = 0
JOIN negociacao_itens ni ON ni.negociacao_id = np.negociacao_id AND ni.categoria_id = np.categoria_id
WHERE np.produtor_origem = '(Fase 1 — histórico)'
  AND NOT EXISTS (
        SELECT 1 FROM embarque_itens ei
        WHERE ei.embarque_id = e.id AND ei.negociacao_produtor_id = np.id
      );

-- 8) CHECK de status só depois do backfill acima, senão o UPDATE do passo 6
--    ficaria refém da própria constraint durante a migração.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_negociacoes_status') THEN
        ALTER TABLE negociacoes ADD CONSTRAINT chk_negociacoes_status
            CHECK (status IN ('EmNegociacao','Fechado','EmEntrega','Concluido'));
    END IF;
END $$;

COMMIT;

-- ============================================================
-- Verificação (apenas leitura)
-- ============================================================
-- SELECT status, COUNT(*) FROM negociacoes GROUP BY status;
-- SELECT COUNT(*) FROM negociacao_produtores WHERE produtor_origem = '(Fase 1 — histórico)';
-- SELECT n.numero, ni.categoria_id, ni.qtd_entregue AS antes_migracao,
--        (SELECT COALESCE(SUM(ei.qtd_chegou),0) FROM embarque_itens ei
--         JOIN negociacao_produtores np ON np.id = ei.negociacao_produtor_id
--         WHERE np.negociacao_id = ni.negociacao_id AND np.categoria_id = ni.categoria_id) AS recalculado_pos_migracao
-- FROM negociacao_itens ni JOIN negociacoes n ON n.id = ni.negociacao_id
-- WHERE ni.qtd_entregue > 0;
