using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.SwaggerGen;
using JWT;
using JWT.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SampleExchangeApi.Console.Attributes;
using SampleExchangeApi.Console.Models;
using SampleExchangeApi.Console.SampleDownload;

namespace SampleExchangeApi.Console.Controllers
{
    /// <inheritdoc />
    /// <summary>
    /// </summary>
    public sealed class SampleApiController : ControllerBase
    {
        private readonly ISampleGetter _sampleGetter;
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sampleGetter"></param>
        /// <param name="logger"></param>
        /// <param name="configuration"></param>
        public SampleApiController(ISampleGetter sampleGetter, ILogger logger, IConfiguration configuration)
        {
            _sampleGetter = sampleGetter;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Download sample
        /// </summary>
        /// <param name="token">download</param>
        /// <response code="200">The Sample</response>
        /// <response code="0">unexpected error</response>
        [HttpGet]
        [Route("/v1/download")]
        [ValidateModelState]
        [SwaggerOperation("DownloadSample")]
        [SwaggerResponse(statusCode: 500, type: typeof(Error),
            description: "We encountered an error while processing the request.")]
        [SwaggerResponse(statusCode: 401, type: typeof(Error), description: "The token is expired.")]
        [SwaggerResponse(statusCode: 400, type: typeof(Error), description: "Bad request.")]
        [SwaggerResponse(statusCode: 200, type: typeof(Sample), description: "The requested sample.")]
        public async Task<IActionResult> DownloadSample([FromQuery] [Required()] string token)
        {
            var partner = string.Empty;

            var correlationToken = Guid.NewGuid().ToString();
            try
            {
                var deserializedToken = new JwtBuilder()
                    .WithSecret(_configuration["Token:Secret"])
                    .MustVerifySignature()
                    .Decode<IDictionary<string, object>>(token);
                var sha256 = deserializedToken["sha256"].ToString();
                partner = deserializedToken["partner"].ToString();
                
                return await _sampleGetter.GetAsync(sha256, partner, correlationToken);
            }
            catch (TokenExpiredException tokenExpiredException)
            {
                _logger.LogWarning(tokenExpiredException, $"Token {token} expired.");
                return StatusCode(401, new Error
                {
                    Code = 401,
                    Message = "The token is expired."
                });
            }
            catch (FormatException formatException)
            {
                _logger.LogError(formatException, $"Bad format. Token: {token}!");
                return StatusCode(400, new Error
                {
                    Code = 400,
                    Message = "Bad request."
                });
            }
            catch (FileNotFoundException fileNotFoundException)
            {
                _logger.LogError(fileNotFoundException, $"File not found. Token: {token}!");
                return StatusCode(404, new Error
                {
                    Code = 404,
                    Message = "File not found."
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Something went wrong. Partner \"{partner}\" got an 500.");
                return StatusCode(500, new Error
                {
                    Code = 500,
                    Message = "We encountered an error while processing the request."
                });
            }
        }
    }
}