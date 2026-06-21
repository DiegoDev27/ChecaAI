using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using ChecaAI.Application.Interfaces;
using ChecaAI.Infrastructure.Data;

namespace ChecaAI.Api.Controllers;

/// <summary>
/// AI-powered endpoints using Claude (Anthropic) for generating summaries,
/// voting analysis, and interactive chat about Brazilian politicians and legislation.
///
/// All endpoints support streaming — use Accept: text/event-stream header to receive
/// progressive output in the Vercel AI SDK data stream format (0:"chunk"\nd:{...}\n).
/// Without the streaming header, a plain JSON response is returned instead.
///
/// Requires AnthropicApiKey in appsettings.json — get yours at console.anthropic.com
/// </summary>
[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly ChecaAIDbContext _db;
    private readonly IClaudeService _claude;
    private readonly ILogger<AiController> _logger;

    private static readonly string SystemBase =
        "Você é um assistente especializado em transparência política brasileira. " +
        "Responda sempre em português, de forma clara e objetiva para o cidadão comum. " +
        "Seja direto, sem jargões políticos desnecessários. " +
        "Quando apresentar números ou valores, use formatação brasileira (R$ e vírgula como separador decimal).";

    public AiController(ChecaAIDbContext db, IClaudeService claude, ILogger<AiController> logger)
    {
        _db = db;
        _claude = claude;
        _logger = logger;
    }

    // ── Politician Analysis ───────────────────────────────────────────────────

    /// <summary>
    /// Generates an AI analysis of a politician's voting profile, expenses, and overall activity.
    /// Streaming: add Accept: text/event-stream to get real-time output.
    /// </summary>
    [HttpGet("politician/{id:int}/analysis")]
    public async Task<IActionResult> AnalyzePoliticianAsync(int id, CancellationToken ct)
    {
        var politician = await _db.Politicians
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (politician == null)
            return NotFound(new { message = "Parlamentar não encontrado" });

        // Build context from DB
        var context = await BuildPoliticianContextAsync(id, ct);

        var systemPrompt = SystemBase +
            "\nVocê tem acesso a dados reais de transparência pública sobre este parlamentar. " +
            "Analise os dados fornecidos e gere um perfil político objetivo, destacando padrões de votação, " +
            "presença, gastos e outras informações relevantes para o cidadão.";

        var userMessage = $"Analise o perfil político do parlamentar com base nos seguintes dados:\n\n{context}";

        return await RespondAsync(systemPrompt, userMessage, ct);
    }

    // ── Proposal Summary ──────────────────────────────────────────────────────

    /// <summary>
    /// Generates a plain-language summary of a legislative proposal.
    /// </summary>
    [HttpGet("proposal/{id:int}/summary")]
    public async Task<IActionResult> SummarizeProposalAsync(int id, CancellationToken ct)
    {
        var proposal = await _db.Proposals
            .Include(p => p.VotingSessions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (proposal == null)
            return NotFound(new { message = "Proposição não encontrada" });

        var context = BuildProposalContext(proposal);

        var systemPrompt = SystemBase +
            "\nVocê explica propostas legislativas de forma simples, sem juridiquês. " +
            "Destaque: o que a proposta faz, quem se beneficia, quem pode ser afetado negativamente, e o status atual.";

        var userMessage = $"Explique esta proposição legislativa de forma clara para um cidadão comum:\n\n{context}";

        return await RespondAsync(systemPrompt, userMessage, ct);
    }

    // ── Voting Session Explanation ────────────────────────────────────────────

    /// <summary>
    /// Explains why a voting session is noteworthy and what was at stake.
    /// </summary>
    [HttpGet("session/{id:int}/explain")]
    public async Task<IActionResult> ExplainSessionAsync(int id, CancellationToken ct)
    {
        var session = await _db.VotingSessions
            .Include(s => s.Proposal)
            .Include(s => s.Alerts)
            .Include(s => s.Votes)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return NotFound(new { message = "Sessão de votação não encontrada" });

        var context = BuildSessionContext(session);

        var systemPrompt = SystemBase +
            "\nVocê explica votações parlamentares de forma clara e imparcial. " +
            "Informe o que foi votado, o resultado, e se houve aspectos suspeitos (horário tardio, urgência, quórum baixo).";

        var userMessage = $"Explique esta votação parlamentar:\n\n{context}";

        return await RespondAsync(systemPrompt, userMessage, ct);
    }

    // ── Interactive Chat ──────────────────────────────────────────────────────

    /// <summary>
    /// Interactive chat about Brazilian politicians and legislation.
    /// Pass the conversation history in the request body.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> ChatAsync([FromBody] ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Mensagem não pode ser vazia" });

        // Build optional context from referenced entities
        var contextBuilder = new StringBuilder();

        if (request.PoliticianId.HasValue)
        {
            var ctx = await BuildPoliticianContextAsync(request.PoliticianId.Value, ct);
            contextBuilder.AppendLine("=== CONTEXTO DO PARLAMENTAR ===");
            contextBuilder.AppendLine(ctx);
        }

        if (request.ProposalId.HasValue)
        {
            var proposal = await _db.Proposals
                .Include(p => p.VotingSessions)
                .FirstOrDefaultAsync(p => p.Id == request.ProposalId.Value, ct);

            if (proposal != null)
            {
                contextBuilder.AppendLine("=== CONTEXTO DA PROPOSIÇÃO ===");
                contextBuilder.AppendLine(BuildProposalContext(proposal));
            }
        }

        var systemPrompt = SystemBase;
        if (contextBuilder.Length > 0)
            systemPrompt += $"\n\nContexto disponível:\n{contextBuilder}";

        return await RespondAsync(systemPrompt, request.Message, ct);
    }

    // ── Compare Politicians ───────────────────────────────────────────────────

    /// <summary>
    /// Compares two politicians side by side.
    /// </summary>
    [HttpGet("compare")]
    public async Task<IActionResult> ComparePoliticiansAsync(
        [FromQuery] int id1, [FromQuery] int id2, CancellationToken ct)
    {
        var p1 = await _db.Politicians.FirstOrDefaultAsync(p => p.Id == id1, ct);
        var p2 = await _db.Politicians.FirstOrDefaultAsync(p => p.Id == id2, ct);

        if (p1 == null || p2 == null)
            return NotFound(new { message = "Um ou mais parlamentares não encontrados" });

        var ctx1 = await BuildPoliticianContextAsync(id1, ct);
        var ctx2 = await BuildPoliticianContextAsync(id2, ct);

        var systemPrompt = SystemBase +
            "\nVocê compara parlamentares objetivamente, com base em dados reais. " +
            "Destaque similaridades, diferenças e pontos importantes para o eleitor.";

        var userMessage = $"Compare os seguintes parlamentares com base nos dados fornecidos:\n\n" +
                          $"### {p1.FullName}\n{ctx1}\n\n### {p2.FullName}\n{ctx2}";

        return await RespondAsync(systemPrompt, userMessage, ct);
    }

    // ── Shared: streaming vs. non-streaming response ──────────────────────────

    private async Task<IActionResult> RespondAsync(
        string systemPrompt, string userMessage, CancellationToken ct)
    {
        var acceptStreaming = Request.Headers.Accept.ToString().Contains("text/event-stream");

        if (acceptStreaming)
        {
            Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
            Response.Headers.Append("Cache-Control", "no-cache");
            Response.Headers.Append("X-Accel-Buffering", "no");

            await _claude.StreamAsync(systemPrompt, userMessage, Response.Body, ct);
            return new EmptyResult();
        }
        else
        {
            var result = await _claude.GenerateAsync(systemPrompt, userMessage, ct);
            return Ok(new { result });
        }
    }

    // ── Context Builders ──────────────────────────────────────────────────────

    private async Task<string> BuildPoliticianContextAsync(int politicianId, CancellationToken ct)
    {
        var politician = await _db.Politicians
            .FirstOrDefaultAsync(p => p.Id == politicianId, ct);

        if (politician == null) return "Parlamentar não encontrado.";

        var sb = new StringBuilder();
        sb.AppendLine($"**{politician.FullName}**");
        sb.AppendLine($"- Cargo: {politician.PoliticalPosition}");
        sb.AppendLine($"- Partido: {politician.Party ?? "N/A"}");
        sb.AppendLine($"- Estado: {politician.State ?? "N/A"}");
        if (!string.IsNullOrWhiteSpace(politician.City))
            sb.AppendLine($"- Município: {politician.City}");
        sb.AppendLine();

        // Recent votes
        var votes = await _db.Votes
            .Where(v => v.PoliticianId == politicianId)
            .Include(v => v.VotingSession)
            .OrderByDescending(v => v.VotingSession.VotingDate)
            .Take(10)
            .ToListAsync(ct);

        if (votes.Count > 0)
        {
            sb.AppendLine("**Últimas votações (10 mais recentes):**");
            var yesCnt = votes.Count(v => v.VoteValue == "Yes");
            var noCnt = votes.Count(v => v.VoteValue == "No");
            var absCnt = votes.Count(v => v.VoteValue == "Abstention");
            var absentCnt = votes.Count(v => v.VoteValue == "Absent");
            sb.AppendLine($"- Sim: {yesCnt} | Não: {noCnt} | Abstenção: {absCnt} | Ausente: {absentCnt}");
            foreach (var vote in votes.Take(5))
                sb.AppendLine($"  • {vote.VotingSession.VotingDate:dd/MM/yyyy}: {vote.VoteValue} — {vote.VotingSession.Description[..Math.Min(80, vote.VotingSession.Description.Length)]}");
            sb.AppendLine();
        }

        // Expenses summary
        var expenseStats = await _db.PoliticianExpenses
            .Where(e => e.PoliticianId == politicianId && e.Year == DateTime.UtcNow.Year)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync(ct);

        if (expenseStats.Count > 0)
        {
            var totalExpenses = expenseStats.Sum(e => e.Total);
            sb.AppendLine($"**Gastos de cota parlamentar em {DateTime.UtcNow.Year}: R$ {totalExpenses:N2}**");
            foreach (var cat in expenseStats)
                sb.AppendLine($"  • {cat.Category}: R$ {cat.Total:N2} ({cat.Count} registros)");
            sb.AppendLine();
        }

        // Salary
        var salary = await _db.PoliticianSalaries
            .Where(s => s.PoliticianId == politicianId)
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
            .FirstOrDefaultAsync(ct);

        if (salary != null)
            sb.AppendLine($"**Remuneração ({salary.Month:D2}/{salary.Year}): bruto R$ {salary.GrossSalary:N2} | líquido R$ {salary.NetSalary:N2}**");

        // Committees
        var committees = await _db.CommitteeMemberships
            .Where(m => m.PoliticianId == politicianId)
            .Include(m => m.Committee)
            .Where(m => m.Committee.IsActive)
            .ToListAsync(ct);

        if (committees.Count > 0)
        {
            sb.AppendLine("**Comissões:**");
            foreach (var m in committees.Take(5))
                sb.AppendLine($"  • {m.Committee.Acronym ?? m.Committee.Name} — {m.Role}");
        }

        return sb.ToString();
    }

    private static string BuildProposalContext(ChecaAI.Domain.Entities.Proposal proposal)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{proposal.Type} {proposal.Number}/{proposal.Year}**");
        sb.AppendLine($"- Câmara: {proposal.Chamber}");
        sb.AppendLine($"- Status: {proposal.Status}");
        if (!string.IsNullOrWhiteSpace(proposal.Author))
            sb.AppendLine($"- Relator/Autor: {proposal.Author}");
        sb.AppendLine();
        sb.AppendLine($"**Ementa:** {proposal.Title}");
        if (!string.IsNullOrWhiteSpace(proposal.Summary))
        {
            sb.AppendLine();
            sb.AppendLine($"**Resumo:** {proposal.Summary}");
        }

        if (proposal.VotingSessions?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**Votações ({proposal.VotingSessions.Count}):**");
            foreach (var session in proposal.VotingSessions.OrderByDescending(s => s.VotingDate).Take(3))
            {
                sb.AppendLine($"  • {session.VotingDate:dd/MM/yyyy}: {session.Result} " +
                              $"(Sim: {session.VotesYes}, Não: {session.VotesNo})");
            }
        }

        return sb.ToString();
    }

    private static string BuildSessionContext(ChecaAI.Domain.Entities.VotingSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Votação em {session.VotingDate:dd/MM/yyyy HH:mm}**");
        sb.AppendLine($"- Casa: {session.Chamber}");
        sb.AppendLine($"- Resultado: {session.Result}");
        sb.AppendLine($"- Tipo: {session.SessionType ?? "N/A"}");
        sb.AppendLine($"- Descrição: {session.Description}");
        sb.AppendLine();
        sb.AppendLine($"**Placar:** Sim {session.VotesYes} | Não {session.VotesNo} | " +
                      $"Abstenção {session.VotesAbstention} | Ausente {session.VotesAbsent}");

        if (session.Proposal != null)
        {
            sb.AppendLine();
            sb.AppendLine($"**Proposição:** {session.Proposal.Type} {session.Proposal.Number}/{session.Proposal.Year}");
            sb.AppendLine($"**Ementa:** {session.Proposal.Title}");
        }

        if (session.Alerts?.Count > 0)
        {
            var alert = session.Alerts.OrderByDescending(a => a.Score).First();
            sb.AppendLine();
            sb.AppendLine($"**Alerta de transparência:** Nível {alert.AlertLevel} (score {alert.Score})");
            if (!string.IsNullOrWhiteSpace(alert.SummaryText))
                sb.AppendLine($"Motivo: {alert.SummaryText}");
        }

        return sb.ToString();
    }

    // ── Request DTOs ──────────────────────────────────────────────────────────

    public sealed class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public int? PoliticianId { get; set; }
        public int? ProposalId { get; set; }
    }
}
