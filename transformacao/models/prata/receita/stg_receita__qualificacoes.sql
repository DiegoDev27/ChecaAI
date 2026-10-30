select
    trim(codigo) as codigo,
    trim(descricao) as descricao
from {{ bronze('receita-cnpj', 'qualificacoes') }}
qualify referencia = max(referencia) over ()
