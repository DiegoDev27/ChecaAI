{{ config(materialized='view', schema='prata') }}

-- Identificador de cada sócio no grafo.
-- Pessoa física: o CPF vem mascarado (***123456**), então a chave é CPF mascarado + nome.
-- Isso NÃO é resolução de entidades: a mesma pessoa pode aparecer com grafias diferentes.
-- A resolução de verdade (cruzar com TSE, CGU…) é uma etapa futura e explícita.
--
-- Sócios sem nome (~155 na fatia 1 de 2026-10, muitos com CPF ***000000**) ficam de fora:
-- sem nome não há identidade, e agrupá-los criaria uma "pessoa" falsa ligada a várias empresas.
select
    s.*,
    case s.tipo_socio
        when 'pessoa_juridica' then 'empresa:' || left(s.documento_socio, 8)
        when 'pessoa_fisica' then 'pessoa:' || md5(coalesce(s.documento_socio, '') || '|' || upper(s.nome_socio))
        else 'pessoa_estrangeira:' || md5(upper(s.nome_socio))
    end as socio_id
from {{ ref('stg_receita__socios') }} s
where s.nome_socio is not null and s.nome_socio <> ''
