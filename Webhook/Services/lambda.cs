using System;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.Lambda;
using Amazon.Lambda.Model;

namespace Services
{
    public class LambdaInvoker
    {
        private readonly AmazonLambdaClient _lambdaClient;
        private readonly string _functionName;

        public LambdaInvoker()
        {
            var regionName = System.Environment.GetEnvironmentVariable("AWS_REGION") ?? "eu-west-1";
            var config = new AmazonLambdaConfig
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(regionName)
            };

            _lambdaClient = new AmazonLambdaClient(config);
            _functionName = System.Environment.GetEnvironmentVariable("AUTOINVENTARIO_LAMBDA_NAME") ?? "Autoinventario";
        }

        public async Task InvokeLambdaAsync(object data)
        {
            var payload = JsonSerializer.Serialize(data);

            var request = new InvokeRequest
            {
                FunctionName = _functionName,
                Payload = payload
            };

            var response = await _lambdaClient.InvokeAsync(request);

            if (response.StatusCode != 200)
            {
                var errorMessage = $"Error al invocar la Lambda. StatusCode: {response.StatusCode}, Error: {response.FunctionError}";
                throw new InvalidOperationException(errorMessage);
            }
        }
    }

}
