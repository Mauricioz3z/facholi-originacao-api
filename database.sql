-- ============================================================
-- PrecoBoi — Script SQL Completo
-- Banco: precogado
-- ============================================================

-- Criar banco (executar conectado ao postgres)
-- CREATE DATABASE precogado;

-- Conectar ao banco precogado antes de executar o restante

-- ============================================================
-- TABELAS
-- ============================================================

CREATE TABLE IF NOT EXISTS usuarios (
    id          SERIAL PRIMARY KEY,
    nome        VARCHAR(150)  NOT NULL,
    email       VARCHAR(200)  UNIQUE NOT NULL,
    senha_hash  VARCHAR(500)  NOT NULL,
    telefone    VARCHAR(30)   DEFAULT '',
    perfil      VARCHAR(20)   NOT NULL CHECK (perfil IN ('Admin', 'Comprador')),
    ativo       BOOLEAN       NOT NULL DEFAULT TRUE,
    criado_em   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS corretores (
    id          SERIAL PRIMARY KEY,
    nome        VARCHAR(200)  NOT NULL,
    telefone    VARCHAR(30)   DEFAULT '',
    municipio   VARCHAR(100)  DEFAULT '',
    uf          VARCHAR(2)    DEFAULT '',
    propriedade VARCHAR(200)  DEFAULT '',
    observacoes TEXT          DEFAULT '',
    ativo       BOOLEAN       NOT NULL DEFAULT TRUE,
    criado_em   TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS municipios_destino (
    id      SERIAL PRIMARY KEY,
    nome    VARCHAR(100) NOT NULL,
    uf      VARCHAR(2)   NOT NULL,
    padrao  BOOLEAN      NOT NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS municipios_origem (
    id           SERIAL PRIMARY KEY,
    nome         VARCHAR(100)    NOT NULL,
    uf           VARCHAR(2)      NOT NULL,
    distancia_km DECIMAL(10,2)   NOT NULL DEFAULT 0,
    valor_km     DECIMAL(10,4)   NOT NULL DEFAULT 0,
    ativo        BOOLEAN         NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS categorias (
    id           SERIAL PRIMARY KEY,
    nome         VARCHAR(50)   NOT NULL,
    peso_min     DECIMAL(10,1) NOT NULL,
    peso_max     DECIMAL(10,1) NOT NULL,
    peso_medio   DECIMAL(10,1) NOT NULL,
    cab_caminhao INTEGER       NOT NULL,
    ordem        INTEGER       NOT NULL
);

CREATE TABLE IF NOT EXISTS icms (
    id           SERIAL PRIMARY KEY,
    uf           VARCHAR(2)    UNIQUE NOT NULL,
    aliquota     DECIMAL(5,2)  NOT NULL DEFAULT 0,
    recuperacao  DECIMAL(5,2)  NOT NULL DEFAULT 0,
    icms_efetivo DECIMAL(8,6)  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS cotacoes_regionais (
    id                  SERIAL PRIMARY KEY,
    uf                  VARCHAR(2)    UNIQUE NOT NULL,
    praca_referencia_uf VARCHAR(2)    DEFAULT NULL,
    valor_arroba        DECIMAL(10,2) NOT NULL DEFAULT 0,
    atualizado_em       TIMESTAMP     DEFAULT NULL
);

CREATE TABLE IF NOT EXISTS agios_cotacao (
    id                  SERIAL PRIMARY KEY,
    cotacao_regional_id INTEGER       NOT NULL REFERENCES cotacoes_regionais(id) ON DELETE CASCADE,
    categoria_id        INTEGER       NOT NULL REFERENCES categorias(id),
    percentual          DECIMAL(5,2)  NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS config_comissao (
    id         SERIAL PRIMARY KEY,
    percentual DECIMAL(5,2) NOT NULL DEFAULT 1.0,
    ativo      BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS negociacoes (
    id                       SERIAL PRIMARY KEY,
    numero                   VARCHAR(20)  NOT NULL UNIQUE,
    comprador_id             INTEGER      REFERENCES usuarios(id),
    corretor_id              INTEGER      REFERENCES corretores(id),
    municipio_origem_id      INTEGER      REFERENCES municipios_origem(id),
    municipio_destino_id     INTEGER      REFERENCES municipios_destino(id),
    data_prevista_entrega    DATE         DEFAULT NULL,
    observacoes              VARCHAR(500) DEFAULT NULL,
    status                   VARCHAR(30)  NOT NULL DEFAULT 'EmNegociacao'
                             CHECK (status IN ('EmNegociacao','Fechado','EmEntrega','Concluido')),
    data_fechamento          TIMESTAMP    DEFAULT NULL,
    tipo_negocio             VARCHAR(10)  NOT NULL DEFAULT 'KG' CHECK (tipo_negocio IN ('Perna','KG')),
    comissao_paga            BOOLEAN      NOT NULL DEFAULT FALSE,
    comissao_paga_em         TIMESTAMP    DEFAULT NULL,
    comissao_paga_por        INTEGER      REFERENCES usuarios(id),
    embarques_ultimo_numero  INTEGER      NOT NULL DEFAULT 0,
    criado_em                TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em            TIMESTAMP    DEFAULT NULL
);

-- Migração: adiciona coluna observacoes em bancos já existentes
ALTER TABLE negociacoes ADD COLUMN IF NOT EXISTS observacoes VARCHAR(500) DEFAULT NULL;

CREATE TABLE IF NOT EXISTS negociacao_itens (
    id             SERIAL PRIMARY KEY,
    negociacao_id  INTEGER       NOT NULL REFERENCES negociacoes(id) ON DELETE CASCADE,
    categoria_id   INTEGER       NOT NULL REFERENCES categorias(id),
    qtd_negociada  INTEGER       DEFAULT NULL,
    preco_negociado DECIMAL(10,4) DEFAULT NULL,
    peso_medio     DECIMAL(10,2) DEFAULT NULL,
    preco_colocado DECIMAL(10,4) DEFAULT NULL,
    qtd_entregue   INTEGER       NOT NULL DEFAULT 0,
    status_entrega VARCHAR(20)   NOT NULL DEFAULT 'Pendente'
);

CREATE TABLE IF NOT EXISTS auditoria (
    id             SERIAL PRIMARY KEY,
    tabela         VARCHAR(100)  NOT NULL,
    registro_id    INTEGER       DEFAULT NULL,
    campo          VARCHAR(100)  NOT NULL DEFAULT '',
    valor_anterior TEXT          DEFAULT NULL,
    valor_novo     TEXT          DEFAULT NULL,
    usuario_id     INTEGER       REFERENCES usuarios(id) ON DELETE SET NULL,
    usuario_nome   VARCHAR(150)  NOT NULL DEFAULT '',
    data_hora      TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    descricao      TEXT          NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS negociacao_contadores (
    ano           INTEGER PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);

-- Seed do contador a partir de negociações já existentes.
-- Idempotente: só insere anos ainda não presentes na tabela de contadores.
INSERT INTO negociacao_contadores (ano, ultimo_numero)
SELECT
    CAST(SPLIT_PART(numero, '/', 2) AS INTEGER) AS ano,
    MAX(CAST(SPLIT_PART(numero, '/', 1) AS INTEGER)) AS ultimo
FROM negociacoes
WHERE numero ~ '^[0-9]+/[0-9]{4}$'
GROUP BY 1
ON CONFLICT (ano) DO NOTHING;

-- ============================================================
-- FASE 2 — Embarques, Chegada e Conferência Administrativa
-- (ver backend/migrations/002_fase2_embarques_chegada_conferencia.sql
-- para a versão com backfill, usada em bancos já existentes)
-- ============================================================

-- Desmembramento por produtor/lote (um lote = um produtor + uma categoria)
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

-- Embarques (carregamento de caminhão, vinculado à negociação e ao lote/produtor)
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

-- Itens do embarque, por categoria — chegada embutida aqui (qtd_chegou,
-- peso_medio_entrada, animais_debilitados), sem tabela própria: é relação
-- 1:1 estrita (um evento de chegada por embarque).
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

-- Conferência administrativa, 1:1 com embarque
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

-- ============================================================
-- ÍNDICES
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_negociacoes_comprador    ON negociacoes(comprador_id);
CREATE INDEX IF NOT EXISTS idx_negociacoes_corretor     ON negociacoes(corretor_id);
CREATE INDEX IF NOT EXISTS idx_negociacoes_status       ON negociacoes(status);
CREATE INDEX IF NOT EXISTS idx_negociacoes_criado_em    ON negociacoes(criado_em DESC);
CREATE INDEX IF NOT EXISTS idx_negociacao_itens_neg     ON negociacao_itens(negociacao_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_tabela         ON auditoria(tabela);
CREATE INDEX IF NOT EXISTS idx_auditoria_usuario        ON auditoria(usuario_id);
CREATE INDEX IF NOT EXISTS idx_auditoria_data_hora      ON auditoria(data_hora DESC);
CREATE INDEX IF NOT EXISTS idx_municipios_origem_uf     ON municipios_origem(uf);
CREATE INDEX IF NOT EXISTS idx_icms_uf                  ON icms(uf);
CREATE INDEX IF NOT EXISTS idx_neg_produtores_negociacao ON negociacao_produtores(negociacao_id);
CREATE INDEX IF NOT EXISTS idx_neg_produtores_neg_cat    ON negociacao_produtores(negociacao_id, categoria_id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_neg_produtores_nome_categoria
    ON negociacao_produtores (negociacao_id, categoria_id, LOWER(TRIM(produtor_origem)));
CREATE INDEX IF NOT EXISTS idx_embarques_negociacao      ON embarques(negociacao_id);
CREATE INDEX IF NOT EXISTS idx_embarque_itens_embarque   ON embarque_itens(embarque_id);
CREATE INDEX IF NOT EXISTS idx_embarque_itens_lote       ON embarque_itens(negociacao_produtor_id);

-- ============================================================
-- DADOS INICIAIS
-- ============================================================

-- Destino padrão: Santo Anastácio-SP
INSERT INTO municipios_destino (nome, uf, padrao) VALUES ('Santo Anastácio', 'SP', TRUE)
ON CONFLICT DO NOTHING;

-- Categorias e faixas de peso (conforme ERS)
INSERT INTO categorias (nome, peso_min, peso_max, peso_medio, cab_caminhao, ordem) VALUES
    ('Bezerro',  200.0, 240.0, 220.0,  90, 1),
    ('Bezerro',  241.0, 270.0, 255.5,  85, 2),
    ('Garrote',  271.0, 300.0, 285.5,  80, 3),
    ('Garrote',  301.0, 330.0, 315.5,  70, 4),
    ('Boi',      331.0, 360.0, 345.5,  65, 5),
    ('Boi',      360.0, 390.0, 375.0,  60, 6)
ON CONFLICT DO NOTHING;

-- ICMS por UF (12% padrão, 7% para RS e SC; recuperação 70%; dentro de SP = 0%)
INSERT INTO icms (uf, aliquota, recuperacao, icms_efetivo) VALUES
    ('AC', 12.00, 70.00, 0.036000),
    ('AL', 12.00, 70.00, 0.036000),
    ('AM', 12.00, 70.00, 0.036000),
    ('AP', 12.00, 70.00, 0.036000),
    ('BA', 12.00, 70.00, 0.036000),
    ('CE', 12.00, 70.00, 0.036000),
    ('DF', 12.00, 70.00, 0.036000),
    ('ES', 12.00, 70.00, 0.036000),
    ('GO', 12.00, 70.00, 0.036000),
    ('MA', 12.00, 70.00, 0.036000),
    ('MG', 12.00, 70.00, 0.036000),
    ('MS', 12.00, 70.00, 0.036000),
    ('MT', 12.00, 70.00, 0.036000),
    ('PA', 12.00, 70.00, 0.036000),
    ('PB', 12.00, 70.00, 0.036000),
    ('PE', 12.00, 70.00, 0.036000),
    ('PI', 12.00, 70.00, 0.036000),
    ('PR', 12.00, 70.00, 0.036000),
    ('RJ', 12.00, 70.00, 0.036000),
    ('RN', 12.00, 70.00, 0.036000),
    ('RO', 12.00, 70.00, 0.036000),
    ('RR', 12.00, 70.00, 0.036000),
    ('RS',  7.00, 70.00, 0.021000),
    ('SC',  7.00, 70.00, 0.021000),
    ('SE', 12.00, 70.00, 0.036000),
    ('SP',  0.00,  0.00, 0.000000),
    ('TO', 12.00, 70.00, 0.036000)
ON CONFLICT (uf) DO NOTHING;

-- Cotações regionais iniciais com ágios padrão (valores de exemplo, admin deve atualizar semanalmente)
INSERT INTO cotacoes_regionais (uf, praca_referencia_uf, valor_arroba, atualizado_em) VALUES
    ('RO', NULL, 330.00, CURRENT_TIMESTAMP),
    ('MT', NULL, 325.00, CURRENT_TIMESTAMP),
    ('MS', NULL, 322.00, CURRENT_TIMESTAMP),
    ('GO', NULL, 320.00, CURRENT_TIMESTAMP),
    ('TO', NULL, 318.00, CURRENT_TIMESTAMP),
    ('PA', NULL, 315.00, CURRENT_TIMESTAMP),
    ('AM', 'RO', 0.00,   CURRENT_TIMESTAMP),
    ('MG', NULL, 310.00, CURRENT_TIMESTAMP),
    ('SP', NULL, 308.00, CURRENT_TIMESTAMP),
    ('RJ', NULL, 308.00, CURRENT_TIMESTAMP),
    ('PR', NULL, 305.00, CURRENT_TIMESTAMP),
    ('RS', NULL, 295.00, CURRENT_TIMESTAMP),
    ('SC', NULL, 296.00, CURRENT_TIMESTAMP),
    ('BA', NULL, 312.00, CURRENT_TIMESTAMP),
    ('MA', NULL, 310.00, CURRENT_TIMESTAMP),
    ('PI', NULL, 308.00, CURRENT_TIMESTAMP),
    ('AC', NULL, 320.00, CURRENT_TIMESTAMP),
    ('RR', NULL, 315.00, CURRENT_TIMESTAMP)
ON CONFLICT (uf) DO NOTHING;

-- Ágios padrão por faixa (após inserção das categorias)
DO $$
DECLARE
    cot_id INTEGER;
    cat_row RECORD;
    agio_val DECIMAL(5,2);
BEGIN
    FOR cot_id IN SELECT id FROM cotacoes_regionais LOOP
        FOR cat_row IN SELECT id, peso_min FROM categorias ORDER BY ordem LOOP
            IF cat_row.peso_min >= 200 AND cat_row.peso_min < 241 THEN agio_val := 30.00;
            ELSIF cat_row.peso_min >= 241 AND cat_row.peso_min < 271 THEN agio_val := 25.00;
            ELSIF cat_row.peso_min >= 271 AND cat_row.peso_min < 301 THEN agio_val := 20.00;
            ELSIF cat_row.peso_min >= 301 AND cat_row.peso_min < 331 THEN agio_val := 15.00;
            ELSE agio_val := 10.00;
            END IF;

            INSERT INTO agios_cotacao (cotacao_regional_id, categoria_id, percentual)
            VALUES (cot_id, cat_row.id, agio_val)
            ON CONFLICT DO NOTHING;
        END LOOP;
    END LOOP;
END $$;

-- Configuração de comissão padrão (1%, ativa)
INSERT INTO config_comissao (percentual, ativo) VALUES (1.0, TRUE)
ON CONFLICT DO NOTHING;

-- Municípios de origem de exemplo (cliente deve fornecer lista completa com distâncias reais)
INSERT INTO municipios_origem (nome, uf, distancia_km, valor_km, ativo) VALUES
    ('Ariquemes',         'RO', 2250.0, 8.50, TRUE),
    ('Ji-Paraná',         'RO', 2150.0, 8.50, TRUE),
    ('Porto Velho',       'RO', 2400.0, 8.50, TRUE),
    ('Vilhena',           'RO', 1950.0, 8.50, TRUE),
    ('Cacoal',            'RO', 2100.0, 8.50, TRUE),
    ('Alta Floresta',     'MT', 1850.0, 8.00, TRUE),
    ('Sinop',             'MT', 1700.0, 8.00, TRUE),
    ('Cuiabá',            'MT', 1550.0, 7.50, TRUE),
    ('Rondonópolis',      'MT', 1450.0, 7.50, TRUE),
    ('Campo Grande',      'MS',  950.0, 7.00, TRUE),
    ('Dourados',          'MS',  900.0, 7.00, TRUE),
    ('Três Lagoas',       'MS',  680.0, 7.00, TRUE),
    ('Goiânia',           'GO', 1050.0, 7.50, TRUE),
    ('Catalão',           'GO',  820.0, 7.50, TRUE),
    ('Palmas',            'TO', 1600.0, 8.00, TRUE),
    ('Araguaína',         'TO', 1750.0, 8.00, TRUE),
    ('Marabá',            'PA', 2100.0, 8.50, TRUE),
    ('Redenção',          'PA', 2000.0, 8.50, TRUE),
    ('Uberlândia',        'MG',  620.0, 6.50, TRUE),
    ('Uberaba',           'MG',  550.0, 6.50, TRUE),
    ('Patos de Minas',    'MG',  700.0, 6.50, TRUE),
    ('Presidente Prudente','SP',  130.0, 6.00, TRUE),
    ('Araçatuba',         'SP',  220.0, 6.00, TRUE),
    ('Barretos',          'SP',  380.0, 6.00, TRUE),
    ('Londrina',          'PR',  500.0, 6.50, TRUE)
ON CONFLICT DO NOTHING;

-- Usuário admin inicial (senha: Admin@2026)
INSERT INTO usuarios (nome, email, senha_hash, telefone, perfil, ativo)
VALUES (
    'Administrador',
    'admin@precoboi.com.br',
    '$2a$11$ActwjEAjncclDAZVijSc2eSyJt8Fk8HcIVRAp4N6kCKH4fEU5HVM.',
    '',
    'Admin',
    TRUE
) ON CONFLICT (email) DO NOTHING;

-- ============================================================
-- SCRIPTS CRUD (exemplos)
-- ============================================================

-- Listar negociações ativas
-- SELECT n.*, u.nome AS comprador, c.nome AS corretor, mo.nome AS origem, mo.uf
-- FROM negociacoes n
-- JOIN usuarios u ON u.id = n.comprador_id
-- JOIN corretores c ON c.id = n.corretor_id
-- JOIN municipios_origem mo ON mo.id = n.municipio_origem_id
-- WHERE n.status = 'EmNegociacao'
-- ORDER BY n.criado_em DESC;

-- Dashboard por comprador
-- SELECT u.nome AS comprador, COUNT(n.id) AS negociacoes,
--        SUM(ni.qtd_negociada) AS total_cabecas
-- FROM negociacoes n
-- JOIN usuarios u ON u.id = n.comprador_id
-- JOIN negociacao_itens ni ON ni.negociacao_id = n.id
-- GROUP BY u.id, u.nome ORDER BY u.nome;

-- Auditoria recente
-- SELECT a.*, u.nome FROM auditoria a
-- LEFT JOIN usuarios u ON u.id = a.usuario_id
-- ORDER BY a.data_hora DESC LIMIT 100;
