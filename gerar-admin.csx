// Script para gerar hash da senha do admin
// Execute com: dotnet script gerar-admin.csx
// Ou use o endpoint POST /api/usuarios para criar via API (necessita de admin existente)

// Senha padrão inicial: Admin@2026
// Hash BCrypt gerado:
var hash = BCrypt.Net.BCrypt.HashPassword("Admin@2026");
Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"SQL: UPDATE usuarios SET senha_hash='{hash}' WHERE email='admin@precoboi.com.br';");

Admin

admin@precoboi.com.br

Admin@2026

comprador

	comprador@facholi.com.br