namespace CaseAcordos.Api.Tratamento
{
    /// <summary>Entrada inválida (mapeada para HTTP 400).</summary>
    public sealed class ValidacaoException(string mensagem) : Exception(mensagem);

    /// <summary>Recurso inexistente (mapeada para HTTP 404).</summary>
    public sealed class RecursoNaoEncontradoException(string mensagem) : Exception(mensagem);

    /// <summary>
    /// Falha de infraestrutura ao consultar o banco (mapeada para HTTP 503).
    /// Isola as camadas superiores dos tipos de exceção do driver (Npgsql).
    /// </summary>
    public sealed class FalhaConsultaException(string mensagem, Exception? interna = null) : Exception(mensagem, interna);

}