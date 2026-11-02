using CaseAcordos.Api.DTO;

namespace CaseAcordos.Testes
{
    public class PaginacaoDtoTests
    {
        [Theory]
        [InlineData(1, 20, 0, 0, false)]
        [InlineData(1, 20, 20, 1, false)]
        [InlineData(1, 20, 21, 2, true)]
        [InlineData(2, 20, 21, 2, false)]
        [InlineData(9, 20, 21, 2, false)] // página além do total: sem próxima
        [InlineData(1, 100, 1000, 10, true)]
        public void Criar_CalculaTotalDePaginasEProxima(
            int pagina, int tamanho, int total, int totalPaginasEsperado, bool temProximaEsperado)
        {
            var dto = PaginacaoDto.Criar(pagina, tamanho, total);

            Assert.Equal(totalPaginasEsperado, dto.TotalPaginas);
            Assert.Equal(temProximaEsperado, dto.PossuiProximaPagina);
            Assert.Equal(total, dto.TotalItens);
        }
    }
}
