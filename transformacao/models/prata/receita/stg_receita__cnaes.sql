select
    trim(codigo) as codigo,
    trim(descricao) as descricao
from {{ bronze('receita-cnpj', 'cnaes') }}
qualify referencia = max(referencia) over ()
