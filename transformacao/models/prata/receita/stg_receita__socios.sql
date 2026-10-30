select
    cnpj_basico,
    case identificador_socio
        when '1' then 'pessoa_juridica'
        when '2' then 'pessoa_fisica'
        when '3' then 'estrangeiro'
    end as tipo_socio,
    trim(nome_socio_razao_social) as nome_socio,
    nullif(trim(cpf_cnpj_socio), '') as documento_socio,
    qualificacao_socio as codigo_qualificacao,
    try_strptime(data_entrada_sociedade, '%Y%m%d')::date as data_entrada_sociedade,
    case faixa_etaria
        when '1' then '0 a 12 anos'
        when '2' then '13 a 20 anos'
        when '3' then '21 a 30 anos'
        when '4' then '31 a 40 anos'
        when '5' then '41 a 50 anos'
        when '6' then '51 a 60 anos'
        when '7' then '61 a 70 anos'
        when '8' then '71 a 80 anos'
        when '9' then 'Mais de 80 anos'
    end as faixa_etaria,
    referencia
from {{ bronze('receita-cnpj', 'socios') }}
qualify referencia = max(referencia) over ()
