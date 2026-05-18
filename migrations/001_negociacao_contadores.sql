-- ============================================================
-- Migração 001: contador atômico de número de negociação
-- ------------------------------------------------------------
-- Substitui a geração baseada em COUNT(*), que produzia
-- duplicatas após exclusões e em criações concorrentes.
--
-- Seguro de rodar mais de uma vez (idempotente).
-- ============================================================

BEGIN;

-- 1) Tabela do contador (uma linha por ano)
CREATE TABLE IF NOT EXISTS negociacao_contadores (
    ano           INTEGER PRIMARY KEY,
    ultimo_numero INTEGER NOT NULL DEFAULT 0
);

-- 2) Seed inicial: para cada ano já presente em negociacoes,
--    define ultimo_numero = MAX(número da sequência).
--    ON CONFLICT garante que rodar novamente não sobrescreve
--    um contador que já avançou após o seed.
INSERT INTO negociacao_contadores (ano, ultimo_numero)
SELECT
    CAST(SPLIT_PART(numero, '/', 2) AS INTEGER) AS ano,
    MAX(CAST(SPLIT_PART(numero, '/', 1) AS INTEGER)) AS ultimo
FROM negociacoes
WHERE numero ~ '^[0-9]+/[0-9]{4}$'
GROUP BY 1
ON CONFLICT (ano) DO NOTHING;

COMMIT;

-- ============================================================
-- Verificação (apenas leitura)
-- ============================================================
-- SELECT * FROM negociacao_contadores ORDER BY ano;
