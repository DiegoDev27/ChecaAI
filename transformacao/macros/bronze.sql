{#- Lê todas as referências de uma tabela da camada bronze. -#}
{% macro bronze(fonte, tabela) -%}
    read_parquet('{{ var("bronze") }}/{{ fonte }}/referencia=*/{{ tabela }}/*.parquet', hive_partitioning = true)
{%- endmacro %}
