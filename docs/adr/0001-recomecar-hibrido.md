# ADR-0001: Recomeçar em repositório novo, reaproveitando peças da v0

- Status: aceita
- Data: 2026-10-29

## Contexto
A v0 (TCC) é uma solução .NET 9 com cerca de 13,5 mil linhas, um schema centrado em `Politician` e cerca de 17 workers em C# gravando direto no PostgreSQL via EF Core. Não tem testes, e controllers e workers acessam o `DbContext` diretamente. A nova visão é um banco relacionado de todo o Estado, com a resolução de entidades como diferencial. Parte do código recente da v0 foi escrita num equipamento de um ex-empregador.

## Decisão
Criar um repositório novo. A v0 é arquivada como `ChecaAI_TCC` (tag `v0-tcc`). Da v0 aproveitamos o **conhecimento**, não o código:
- o catálogo de fontes e as armadilhas de cada uma (registradas em `catalogo/fontes.yaml`);
- o modelo hierárquico do Estado (`modelo/estado-brasileiro.json`);
- as regras de pontuação de alertas de votação;
- o domínio checa.ai e o nome.

## Alternativas consideradas
- **Evoluir a v0 no lugar.** O site voltaria ao ar mais rápido, mas o schema e os workers seriam reescritos de qualquer forma, sem testes como rede de proteção.
- **Recomeçar do zero sem olhar a v0.** Perderia meses de aprendizado sobre as APIs do governo.

## Consequências
- (+) Base limpa, arquitetura alinhada à visão e sem dúvida de propriedade intelectual.
- (+) Um artefato novo e bem definido para o artigo (Design Science Research).
- (−) O site fica fora do ar até o primeiro walking skeleton.
