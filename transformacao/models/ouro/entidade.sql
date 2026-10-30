with matrizes as (
    select * from {{ ref('stg_receita__estabelecimentos') }} where matriz
),

contagem_estabelecimentos as (
    select cnpj_basico, count(*) as qtd_estabelecimentos
    from {{ ref('stg_receita__estabelecimentos') }}
    group by all
),

empresas as (
    select
        'empresa:' || e.cnpj_basico as id,
        'empresa' as tipo,
        e.razao_social as nome,
        coalesce(m.cnpj, e.cnpj_basico) as documento,
        json_object(
            'cnpj_basico', e.cnpj_basico,
            'nome_fantasia', m.nome_fantasia,
            'situacao_cadastral', m.situacao_cadastral,
            'data_situacao_cadastral', m.data_situacao_cadastral,
            'data_inicio_atividade', m.data_inicio_atividade,
            'cnae_principal', json_object('codigo', m.codigo_cnae_principal, 'descricao', cnae.descricao),
            'natureza_juridica', json_object('codigo', e.codigo_natureza_juridica, 'descricao', nat.descricao),
            'porte', e.porte,
            'capital_social', e.capital_social,
            'endereco', json_object(
                'logradouro', m.logradouro, 'numero', m.numero, 'complemento', m.complemento,
                'bairro', m.bairro, 'cep', m.cep, 'municipio', mun.descricao, 'uf', m.uf
            ),
            'qtd_estabelecimentos', ce.qtd_estabelecimentos
        ) as atributos,
        e.referencia
    from {{ ref('stg_receita__empresas') }} e
    left join matrizes m using (cnpj_basico)
    left join contagem_estabelecimentos ce using (cnpj_basico)
    left join {{ ref('stg_receita__cnaes') }} cnae on cnae.codigo = m.codigo_cnae_principal
    left join {{ ref('stg_receita__naturezas') }} nat on nat.codigo = e.codigo_natureza_juridica
    left join {{ ref('stg_receita__municipios') }} mun on mun.codigo = m.codigo_municipio
),

-- Empresas que aparecem como sócias mas cujo cadastro não está nesta carga
-- (no walking skeleton só carregamos uma fatia). Viram nós "rasos" para o grafo fechar.
empresas_socias_fora_da_carga as (
    select
        socio_id as id,
        'empresa' as tipo,
        any_value(nome_socio) as nome,
        any_value(documento_socio) as documento,
        json_object('parcial', true) as atributos,
        any_value(referencia) as referencia
    from {{ ref('int_socio_id') }}
    where tipo_socio = 'pessoa_juridica'
      and socio_id not in (select id from empresas)
    group by socio_id
),

-- Mesmo caso para as empresas dos sócios: o sócio está na fatia de Sócios, mas a empresa não está na de Empresas.
empresas_de_socios_fora_da_carga as (
    select
        'empresa:' || cnpj_basico as id,
        'empresa' as tipo,
        null as nome,
        cnpj_basico as documento,
        json_object('parcial', true) as atributos,
        any_value(referencia) as referencia
    from {{ ref('int_socio_id') }}
    where 'empresa:' || cnpj_basico not in (select id from empresas)
    group by cnpj_basico
),

pessoas as (
    select
        socio_id as id,
        case tipo_socio when 'pessoa_fisica' then 'pessoa' else 'pessoa_estrangeira' end as tipo,
        any_value(nome_socio) as nome,
        any_value(documento_socio) as documento,
        json_object('faixa_etaria', any_value(faixa_etaria)) as atributos,
        any_value(referencia) as referencia
    from {{ ref('int_socio_id') }}
    where tipo_socio in ('pessoa_fisica', 'estrangeiro')
    group by socio_id, tipo_socio
),

todas as (
    select * from empresas
    union all by name
    select * from empresas_socias_fora_da_carga
    union all by name
    select * from empresas_de_socios_fora_da_carga
    where id not in (select id from empresas_socias_fora_da_carga)
    union all by name
    select * from pessoas
)

select
    id,
    tipo,
    nome,
    documento,
    atributos,
    'receita-cnpj' as fonte,
    referencia
from todas
