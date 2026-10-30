select
    trim(codigo) as codigo,
    trim(descricao) as descricao
from {{ bronze('receita-cnpj', 'municipios') }}
qualify referencia = max(referencia) over ()
