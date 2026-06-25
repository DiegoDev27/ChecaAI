import { streamText } from 'ai';
import { createAnthropic } from '@ai-sdk/anthropic';

const anthropic = createAnthropic({
  apiKey: process.env.ANTHROPIC_API_KEY ?? '',
});

const SYSTEM_PROMPT = `Você é o assistente de transparência política do ChecaAI, uma plataforma brasileira de monitoramento parlamentar.

Seu papel é ajudar cidadãos a entender:
- Como parlamentares brasileiros votam (deputados federais, senadores, deputados estaduais, vereadores)
- Gastos públicos: cota parlamentar (CEAP), salários, auxílios, assessores
- Propostas legislativas: projetos de lei, medidas provisórias, emendas constitucionais
- Alertas de votações suspeitas: madrugada, regime de urgência, quórum baixo
- Partidos políticos, filiações e alianças
- Resultados eleitorais e histórico dos candidatos

Seja objetivo, direto e use linguagem acessível. Quando relevante, mencione que o usuário pode pesquisar detalhes na plataforma. Use dados reais do congresso brasileiro quando souber.

Se o usuário perguntar sobre um político específico, explique como consultá-lo na aba "Parlamentares".
Se perguntar sobre uma votação, explique como encontrá-la em "Votações".
Se perguntar sobre gastos, mencione que estão disponíveis no perfil do parlamentar.

Responda sempre em português brasileiro.`;

export async function POST(req: Request) {
  try {
    const { messages } = await req.json();

    const result = await streamText({
      model: anthropic('claude-3-5-haiku-20241022'),
      system: SYSTEM_PROMPT,
      messages,
      maxTokens: 1024,
    });

    return result.toDataStreamResponse();
  } catch (error) {
    console.error('[AI Chat] Error:', error);
    return new Response('Erro ao processar sua pergunta. Verifique a configuração da API.', {
      status: 500,
    });
  }
}
