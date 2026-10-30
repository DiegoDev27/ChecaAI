select
    cnpj_basico,
    trim(razao_social) as razao_social,
    natureza_juridica as codigo_natureza_juridica,
    qualificacao_responsavel as codigo_qualificacao_responsavel,
    try_cast(replace(capital_social, ',', '.') as decimal(18, 2)) as capital_social,
    case porte_empresa
        when '01' then 'Microempresa'
        when '03' then 'Empresa de pequeno porte'
        when '05' then 'Demais'
        else 'Não informado'
    end as porte,
    nullif(trim(ente_federativo_responsavel), '') as ente_federativo_responsavel,
    referencia
from {{ bronze('receita-cnpj', 'empresas') }}
qualify referencia = max(referencia) over ()
