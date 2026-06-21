import { streamText } from 'ai';
import { createAnthropic } from '@ai-sdk/anthropic';

const anthropic = createAnthropic({
  apiKey: process.env.ANTHROPIC_API_KEY ?? '',
});

const BACKEND_URL = process.env.API_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'https://localhost:7001';

async function fetchSessionData(id: string) {
  try {
    const res = await fetch(`${BACKEND_URL}/api/sessions/${id}`, {
      headers: { 'Accept': 'application/json' },
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
  const session = await fetchSessionData(id);

  let context = '';
  if (session) {
    const summary = session.votesSummary ?? {};
    context = `
Votação no ${session.chamber ?? 'Congresso'}:
- Descrição: ${session.description}
- Data: ${session.votingDate ? new Date(session.votingDate).toLocaleDateString('pt-BR') : 'N/A'}
- Resultado: ${session.result}
- Tipo: ${session.sessionType ?? 'Ordinário'}
- Votos Sim: ${summary.yes ?? session.votesYes ?? 0}
- Votos Não: ${summary.no ?? session.votesNo ?? 0}
- Abstenções: ${summary.abstention ?? session.votesAbstention ?? 0}
- Ausentes: ${summary.absent ?? session.votesAbsent ?? 0}
- Total de votos: ${session.totalVotes ?? 0}
${session.proposal ? `- Proposição: ${session.proposal.type} ${session.proposal.number}/${session.proposal.year} — ${session.proposal.title} (Status: ${session.proposal.status})` : ''}
`.trim();
  }

  const prompt = `${context ? `${context}\n\n` : ''}Explique esta votação para o cidadão brasileiro comum, em português simples e direto. Aborde:
1. O que foi votado e seu impacto prático na vida dos brasileiros
2. O resultado e o que significa
3. Contexto político (se o horário, tipo de sessão ou margem de votos for relevante)
4. Por que o cidadão deveria se importar com essa votação

Use linguagem acessível, sem jargão jurídico. Seja objetivo e imparcial. Se não tiver dados suficientes, explique o que foi possível compreender.`;

  try {
    const result = await streamText({
      model: anthropic('claude-3-5-haiku-20241022'),
      prompt,
      maxTokens: 600,
    });

    return result.toDataStreamResponse();
  } catch (error) {
    console.error('[AI Session] Error:', error);
    return new Response('Erro ao gerar explicação.', { status: 500 });
  }
}
