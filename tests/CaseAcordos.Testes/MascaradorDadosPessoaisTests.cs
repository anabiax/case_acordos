using CaseAcordos.Api.Mascaramento;

namespace CaseAcordos.Testes
{
    public class MascaradorDadosPessoaisTests
    {
        [Theory]
        [InlineData("Leanne Graham", "L*** G***")]
        [InlineData("  Maria   Silva Souza ", "M*** S*** S***")]
        [InlineData("Ana", "A***")]
        public void MascararNome_MantemApenasIniciais(string nome, string esperado)
            => Assert.Equal(esperado, MascaradorDadosPessoais.MascararNome(nome));

        [Theory]
        [InlineData("joao.silva@empresa.com", "j***@empresa.com")]
        [InlineData("sem-arroba", "***")]
        [InlineData("@dominio.com", "***")]
        public void MascararEmail_MantemDominio(string email, string esperado)
            => Assert.Equal(esperado, MascaradorDadosPessoais.MascararEmail(email));

        [Theory]
        [InlineData("(11) 99999-0000", "(**) *****-0000")]
        [InlineData("1-770-736-8031", "*-***-***-8031")]
        [InlineData("123", "123")]
        public void MascararTelefone_MantemSeparadoresEUltimosQuatroDigitos(string telefone, string esperado)
            => Assert.Equal(esperado, MascaradorDadosPessoais.MascararTelefone(telefone));

        [Fact]
        public void MascararEndereco_OcultaPorCompleto()
            => Assert.Equal("********", MascaradorDadosPessoais.MascararEndereco("Rua X, Apto 10 - Cidade - CEP 00000-000"));

        [Fact]
        public void Mascarar_ComNuloOuVazio_DevolveOValorOriginal()
        {
            Assert.Null(MascaradorDadosPessoais.MascararNome(null));
            Assert.Null(MascaradorDadosPessoais.MascararEmail(null));
            Assert.Equal("", MascaradorDadosPessoais.MascararTelefone(""));
            Assert.Equal(" ", MascaradorDadosPessoais.MascararEndereco(" "));
        }
    }
}