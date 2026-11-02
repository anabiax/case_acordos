using System.Text;

namespace CaseAcordos.Api.Mascaramento
{
    /// <summary>
    /// Funções puras para mascarar dados pessoais (LGPD) em logs e/ou respostas da API.
    /// Todas aceitam nulo/vazio e devolvem o valor como veio.
    /// </summary>
    public static class MascaradorDadosPessoais
    {
        private const char Mascara = '*';
        private const string EnderecoOculto = "********";

        /// <summary>"Leanne Graham" → "L*** G***".</summary>
        public static string? MascararNome(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return nome;

            var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => $"{p[0]}***");
            return string.Join(' ', partes);
        }

        /// <summary>"joao.silva@empresa.com" → "j***@empresa.com" (o domínio é mantido).</summary>
        public static string? MascararEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return email;

            var arroba = email.IndexOf('@');
            return arroba <= 0
                ? "***"
                : $"{email[0]}***{email[arroba..]}";
        }

        /// <summary>Mantém separadores e apenas os 4 últimos dígitos: "(11) 99999-0000" → "(**) *****-0000".</summary>
        public static string? MascararTelefone(string? telefone, int digitosVisiveis = 4)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return telefone;

            var totalDigitos = telefone.Count(char.IsDigit);
            var digitosParaOcultar = Math.Max(0, totalDigitos - digitosVisiveis);

            var resultado = new StringBuilder(telefone.Length);
            var vistos = 0;
            foreach (var c in telefone)
            {
                if (char.IsDigit(c))
                {
                    resultado.Append(vistos < digitosParaOcultar ? Mascara : c);
                    vistos++;
                }
                else
                {
                    resultado.Append(c);
                }
            }

            return resultado.ToString();
        }

        /// <summary>O endereço completo é oculto por inteiro (rua, complemento e CEP identificam a residência).</summary>
        public static string? MascararEndereco(string? endereco)
            => string.IsNullOrWhiteSpace(endereco) ? endereco : EnderecoOculto;
    }
}