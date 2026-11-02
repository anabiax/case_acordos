using Amazon;
using Amazon.Runtime;
using Amazon.SQS;

namespace CaseAcordos.Mensageria;

public static class FabricaSqs
{
    public static IAmazonSQS Criar(OpcoesFilas opcoes)
    {
        if (string.IsNullOrWhiteSpace(opcoes.UrlServicoSqs))
        {
            // AWS real: credenciais pela cadeia padrão (perfil, variáveis de ambiente, role da tarefa/Lambda).
            return new AmazonSQSClient(RegionEndpoint.GetBySystemName(opcoes.Regiao));
        }

        var configuracao = new AmazonSQSConfig
        {
            ServiceURL = opcoes.UrlServicoSqs,
            AuthenticationRegion = opcoes.Regiao
        };

        // LocalStack aceita qualquer credencial; "test" é o valor convencional e não é um segredo.
        var credenciais = new BasicAWSCredentials(opcoes.ChaveAcesso ?? "test", opcoes.ChaveSecreta ?? "test");
        return new AmazonSQSClient(credenciais, configuracao);
    }
}
