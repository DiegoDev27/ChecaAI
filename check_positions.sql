SELECT "PoliticalPosition", COUNT(*) as total, COUNT("Cpf") as with_cpf, COUNT("ExternalId") as with_ext_id FROM "Politicians" GROUP BY "PoliticalPosition" ORDER BY 2 DESC;
