using System.ComponentModel.DataAnnotations;

namespace CaseAcordos.Api.Configuracao
{
    /// <summary>
    /// Autenticação por API Key fixa. O valor NUNCA fica no código nem no appsettings:
    /// vem da variável de ambiente <c>Autenticacao__ApiKey</c>.
    /// </summary>
    public sealed class OpcoesAutenticacao
    {
        public const string Secao = "Autenticacao";
        public const string NomeCabecalhoPadrao = "X-Api-Key";

        [Required(ErrorMessage = "Autenticacao:ApiKey não configurada (defina a variável de ambiente Autenticacao__ApiKey).")]
        [MinLength(16, ErrorMessage = "Autenticacao:ApiKey deve ter ao menos 16 caracteres.")]
        public string ApiKey { get; set; } = string.Empty;

        [Required]
        public string NomeCabecalho { get; set; } = NomeCabecalhoPadrao;
    }

    /// <summary>String de conexão do PostgreSQL (variável de ambiente <c>ConnectionStrings__Acordos</c>).</summary>
    public sealed class OpcoesBanco
    {
        public const string NomeConexao = "Acordos";

        [Required(ErrorMessage = "ConnectionStrings:Postgres não configurada (defina a variável de ambiente ConnectionStrings__Acordos).")]
        public string StringConexao { get; set; } = string.Empty;
    }

    public sealed class OpcoesSwagger
    {
        public const string Secao = "Swagger";

        public bool Habilitado { get; set; } = true;
    }

    public sealed class OpcoesMascaramento
    {
        public const string Secao = "Mascaramento";

        /// <summary>
        /// Quando verdadeiro, nome, e-mail, telefone e endereço do autor são mascarados na resposta
        /// (útil para ambientes não produtivos ou consumidores sem necessidade do dado completo).
        /// </summary>
        public bool MascararRespostas { get; set; }
    }
}