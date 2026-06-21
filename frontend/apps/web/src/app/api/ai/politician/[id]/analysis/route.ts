import { streamText } from 'ai';
import { createAnthropic } from '@ai-sdk/anthropic';

const anthropic = createAnthropic({
  apiKey: process.env.ANTHROPIC_API_KEY ?? '',
});

const BACKEND_URL = process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001';

async function fetchPoliticianData(id: string) {
  try {
    const res = await fetch(`${BACKEND_URL}/api/politicians/${id}`, {
      headers: { 'Accept': 'application/json' },
      // skip TLS verification in dev
      ...(process.env.NODE_ENV === 'development' ? {} : {}),
    });
    if (!res.ok) return null;
    return res.json();
  } catch {
    return null;
  }
}

export async function POST(
  _req: Request,
  { params }: { params: { id: string } },
) {
  const id = params.id;
  const data = await fetchPoliticianData(id);

  let context = '';
  if (data) {
    context = `
Dados reais do parlamentar:
- Nome: ${data.fullName}
- Cargo: ${data.politicalPosition}
- Partido: ${data.party ?? 'não informado'}
- Estado: ${data.state ?? 'não informado'}
- Ativo: ${data.isActive ? 'Sim' : 'Não'}
- Total de votos registrados: ${data.voteStats?.total ?? 'N/A'}
- Votos Sim: ${data.voteStats?.yes ?? 'N/A'} | Não: ${data.voteStats?.no ?? 'N/A'} | Abstenção: ${data.voteStats?.abstention ?? 'N/A'} | Ausente: ${data.voteStats?.absent ?? 'N/A'}
- Último salário bruto registrado: ${data.latestSalary?.grossSalary ? `R$ ${data.latestSalary.grossSalary.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` : 'N/A'}
- Gastos recentes com cota parlamentar: ${data.expenseSummary?.totalExpenses ? `R$ ${data.expenseSummary.totalExpenses.toLocaleString('pt-BR', { minimumFractionDigits: 2 })}` : 'N/A'}
- Comissões: ${(data.committees ?? []).map((c: { name: string }) => c.name).join(', ') || 'N/A'}
`.trim();
  }

  const prompt = `${context ? `${context}\n\n` : ''}Faça uma análise objetiva deste parlamentar para o cidadão brasileiro, em português. Aborde:
1. Perfil geral e cargo
2. Comportamento de voto (padrão baseado nos dados)
3. Transparência financeira (salário, gastos de cota)
4. Atuação em comissões
5. O que o cidadão deve saber sobre este político

Seja imparcial, cite os dados reais fornecidos e use linguagem acessível. Se não tiver dados suficientes, seja honesto sobre as limitações.`;

  try {
    const result = await streamText({
      model: anthropic('claude-3-5-haiku-20241022'),
      prompt,
      maxTokens: 800,
    });

    return result.toDataStreamResponse();
  } catch (error) {
    console.error('[AI Politician] Error:', error);
    return new Response('Erro ao gerar análise.', { status: 500 });
  }
}
