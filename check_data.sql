SELECT 'politicians' as tbl, COUNT(*) as cnt FROM "Politicians"
UNION ALL SELECT 'expenses', COUNT(*) FROM "PoliticianExpenses"
UNION ALL SELECT 'votes', COUNT(*) FROM "Votes"
UNION ALL SELECT 'voting_sessions', COUNT(*) FROM "VotingSessions"
UNION ALL SELECT 'salaries', COUNT(*) FROM "PoliticianSalaries"
UNION ALL SELECT 'election_results', COUNT(*) FROM "ElectionResults"
UNION ALL SELECT 'attendances', COUNT(*) FROM "SessionAttendances"
UNION ALL SELECT 'committees', COUNT(*) FROM "Committees"
UNION ALL SELECT 'campaign_expenses', COUNT(*) FROM "CampaignExpenses";
