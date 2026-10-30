select
    s.socio_id as origem_id,
    'empresa:' || s.cnpj_basico as destino_id,
    'socio_de' as tipo,
    json_object(
        'qualificacao', q.descricao,
        'data_entrada', s.data_entrada_sociedade
    ) as atributos,
    'receita-cnpj' as fonte,
    s.referencia
from {{ ref('int_socio_id') }} s
left join {{ ref('stg_receita__qualificacoes') }} q on q.codigo = s.codigo_qualificacao
