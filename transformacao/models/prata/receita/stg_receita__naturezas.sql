select
    trim(codigo) as codigo,
    trim(descricao) as descricao
from {{ bronze('receita-cnpj', 'naturezas') }}
qualify referencia = max(referencia) over ()
