SELECT "PoliticalPosition", COUNT(*) as total, COUNT("Cpf") as with_cpf FROM "Politicians" GROUP BY "PoliticalPosition" ORDER BY 2 DESC LIMIT 15;
