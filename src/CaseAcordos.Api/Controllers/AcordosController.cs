using System.ComponentModel.DataAnnotations;
using CaseAcordos.Api.Configuracao;
using CaseAcordos.Api.DTO;
using Microsoft.AspNetCore.Mvc;

namespace CaseAcordos.Api.Controllers
{
    [ApiController]
    [Route("api/v1/usuarios")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public sealed class AcordosController(IServicoConsultaAcordos servico) : ControllerBase
    {
        /// <summary>
        /// Retorna de forma consolidada os dados do autor, o resumo, os acordos e os documentos de cada acordo.
        /// </summary>
        /// <remarks>
        /// Os documentos são paginados <b>por acordo</b> (padrão: 20 por acordo, máximo 100). Cada acordo informa
        /// <c>quantidadeDocumentos</c> (total) e <c>paginacaoDocumentos</c>. Para navegar nos documentos de um acordo
        /// com grande volume, use <c>GET /api/v1/usuarios/{id}/acordos/{acordoId}/documentos</c>.
        /// </remarks>
        /// <param name="id">Identificador do autor (inteiro positivo).</param>
        /// <param name="paginaDocumentos">Página de documentos aplicada a cada acordo (base 1).</param>
        /// <param name="tamanhoPaginaDocumentos">Quantidade de documentos por acordo na página (1 a 100).</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Consulta realizada com sucesso.</response>
        /// <response code="400">Id do autor ou parâmetros de paginação inválidos.</response>
        /// <response code="404">Autor não encontrado.</response>
        /// <response code="503">Base de dados indisponível.</response>
        [HttpGet("{id}/acordos")]
        [ProducesResponseType(typeof(ConsultaAcordosDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<ConsultaAcordosDto>> ObterAcordosDoAutor([FromRoute, Range(1, int.MaxValue)] int id,
            [FromQuery, Range(LimitesPaginacao.PaginaMinima, LimitesPaginacao.PaginaMaxima)]
                int paginaDocumentos = LimitesPaginacao.PaginaMinima,
            [FromQuery, Range(LimitesPaginacao.TamanhoMinimo, LimitesPaginacao.TamanhoMaximo)]
                int tamanhoPaginaDocumentos = LimitesPaginacao.TamanhoPadrao, CancellationToken cancellationToken = default)
        {
            var resposta = await servico.ConsultarPorAutorAsync(id, paginaDocumentos, tamanhoPaginaDocumentos, cancellationToken);

            return Ok(resposta);
        }



        /// <summary>Lista, de forma paginada, os documentos de um acordo do autor.</summary>
        /// <param name="id">Identificador do autor (inteiro positivo).</param>
        /// <param name="acordoId">Identificador do acordo (inteiro positivo); deve pertencer ao autor.</param>
        /// <param name="pagina">Página (base 1).</param>
        /// <param name="tamanhoPagina">Itens por página (1 a 100).</param>
        /// <param name="cancellationToken"></param>
        /// <response code="200">Página de documentos (vazia se a página estiver além do total).</response>
        /// <response code="400">Ids ou parâmetros de paginação inválidos.</response>
        /// <response code="404">Acordo não encontrado para o autor informado.</response>
        /// <response code="503">Base de dados indisponível.</response>
        [HttpGet("{id}/acordos/{acordoId}/documentos")]
        [ProducesResponseType(typeof(PaginaDocumentosDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<PaginaDocumentosDto>> ObterDocumentosDoAcordo([FromRoute, Range(1, int.MaxValue)] int id,
            [FromRoute, Range(1, int.MaxValue)] int acordoId,
            [FromQuery, Range(LimitesPaginacao.PaginaMinima, LimitesPaginacao.PaginaMaxima)]
                int pagina = LimitesPaginacao.PaginaMinima,
            [FromQuery, Range(LimitesPaginacao.TamanhoMinimo, LimitesPaginacao.TamanhoMaximo)]
                int tamanhoPagina = LimitesPaginacao.TamanhoPadrao, CancellationToken cancellationToken = default)
        {
            var resposta = await servico.ConsultarDocumentosDoAcordoAsync(id, acordoId, pagina, tamanhoPagina, cancellationToken);

            return Ok(resposta);
        }
    }
}