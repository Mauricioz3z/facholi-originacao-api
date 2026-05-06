// Script para gerar hash da senha do admin
// Execute com: dotnet script gerar-admin.csx
// Ou use o endpoint POST /api/usuarios para criar via API (necessita de admin existente)

// Senha padrão inicial: Admin@2026
// Hash BCrypt gerado:
var hash = BCrypt.Net.BCrypt.HashPassword("Admin@2026");
Console.WriteLine($"Hash: {hash}");
Console.WriteLine($"SQL: UPDATE usuarios SET senha_hash='{hash}' WHERE email='admin@precoboi.com.br';");

Credencias adminsitrador

Email: admin@precoboi.com.br

Senha: Admin@2026
Credencias Comprador
 Email: comprador@facholi.com.br
 senha:facholi123

Apara o comprador tem aparencia de aplicativo 

Para o adminsitrador tem aparencia desktop (computador)


link de acesso: https://app-precogado.wdevdigital.com.br/login

para adicionar na ela inicial do  android: 


para adicionar na ela inicial do  IOS: 



	