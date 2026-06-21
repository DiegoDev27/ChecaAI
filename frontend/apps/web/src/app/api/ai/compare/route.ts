import { streamText } from 'ai';
import { createAnthropic } from '@ai-sdk/anthropic';

const anthropic = createAnthropic({
  apiKey: process.env.ANTHROPIC_API_KEY ?? '',
});

const BACKEND_URL = process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001';

async function fetchPolitician(id: string) {
  try {
    const res = await fetch(`${BACKEND_URL}/api/politicians/${id}`, {
      headers: { Accept: 'application/json' },
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export async function POST(req: Request) {
  const { id1, id2, agreePercent, commonCount } = await req.json();

  const [p1, p2] = await Promise.all([fetchPolitician(id1), fetchPolitician(id2)]);

  const fmt = (n: number | undefined) =>
    n !== undefined ? `R$ ${n.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` : 'N/A';

  const describeVotes = (p: Record<string, unknown> | null) => {
    if (!p?.voteStats) return 'dados não disponíveis';
    const vs = p.voteStats as Record<string, number>;
    const total = vs.total || 1;
    return `${((vs.yes / total) * 100).toFixed(1)}% Sim, ${((vs.no / total) * 100).toFixed(1)}% Não, presença ${(vs.presenceRate ?? 0).toFixed(1)}%`;
  };

  const context = `
Comparativo entre dois parlamentares brasileiros:

PARLAMENTAR 1: ${p1?.fullName ?? 'N/A'}
- Cargo: ${p1?.politicalPosition ?? 'N/A'}
- Partido: ${p1?.party ?? 'N/A'}
- Estado: ${p1?.state ?? 'N/A'}
- Padrão de votos: ${describeVotes(p1)}
- Salário bruto: ${fmt(p1?.latestSalary?.grossSalary)}
- Despesas CEAP: ${fmt(p1?.expenseSummary?.total)}

PARLAMENTAR 2: ${p2?.fullName ?? 'N/A'}
- Cargo: ${p2?.politicalPosition ?? 'N/A'}
- Partido: ${p2?.party ?? 'N/A'}
- Estado: ${p2?.state ?? 'N/A'}
- Padrão de votos: ${describeVotes(p2)}
- Salário bruto: ${fmt(p2?.latestSalary?.grossSalary)}
- Despesas CEAP: ${fmt(p2?.expenseSummary?.total)}

VOTAÇÕES EM COMUM: ${commonCount ?? 0} votações analisadas
CONCORDÂNCIA: ${agreePercent ?? 0}% votaram da mesma forma
`.trim();

  const prompt = `${context}

Com base nesses dados reais, faça uma análise comparativa objetiva e imparcial desses dois parlamentares para o cidadão brasileiro. Aborde:
1. Principais diferenças no perfil e posicionamento político
2. Comportamento de voto: como cada um vota e no que concordam/divergem
3. Gastos públicos: quem gasta mais e como isso se compara à média
4. O que o cidadão deve saber ao comparar esses dois parlamentares

Use linguagem acessível, seja imparcial e cite os dados fornecidos. Responda em português brasileiro.`;

  try {
    const result = await streamText({
      model: anthropic('claude-3-5-haiku-20241022'),
      prompt,
      maxTokens: 800,
    });

    return result.toDataStreamResponse();
  } catch (error) {
    console.error('[AI Compare] Error:', error);
    return new Response('Erro ao gerar comparativo.', { status: 500 });
  }
}
