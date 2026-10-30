select
    cnpj_basico || cnpj_ordem || cnpj_dv as cnpj,
    cnpj_basico,
    identificador_matriz_filial = '1' as matriz,
    nullif(trim(nome_fantasia), '') as nome_fantasia,
    case situacao_cadastral
        when '01' then 'Nula'
        when '02' then 'Ativa'
        when '03' then 'Suspensa'
        when '04' then 'Inapta'
        when '08' then 'Baixada'
    end as situacao_cadastral,
    try_strptime(nullif(data_situacao_cadastral, '0'), '%Y%m%d')::date as data_situacao_cadastral,
    motivo_situacao_cadastral as codigo_motivo_situacao,
    try_strptime(data_inicio_atividade, '%Y%m%d')::date as data_inicio_atividade,
    cnae_fiscal_principal as codigo_cnae_principal,
    nullif(cnae_fiscal_secundaria, '') as codigos_cnae_secundarios,
    regexp_replace(concat_ws(' ', nullif(trim(tipo_logradouro), ''), nullif(trim(logradouro), '')), '\s+', ' ', 'g') as logradouro,
    nullif(trim(numero), '') as numero,
    nullif(regexp_replace(trim(complemento), '\s+', ' ', 'g'), '') as complemento,
    nullif(trim(bairro), '') as bairro,
    cep,
    uf,
    municipio as codigo_municipio,
    nullif(trim(situacao_especial), '') as situacao_especial,
    referencia
from {{ bronze('receita-cnpj', 'estabelecimentos') }}
qualify referencia = max(referencia) over ()
