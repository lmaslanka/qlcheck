using Qlcheck.Languages.CSharp.Catalog;

namespace Qlcheck.Tests;

public class SsrfPatternsTests
{
    [Fact]
    public void Ssrf_flags_a_raw_parameter_passed_directly_to_an_http_client_method()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                private readonly HttpClient httpClient = new();

                public Task<string> UnsafeAsync(string userSuppliedUrl, CancellationToken cancellationToken) =>
                    httpClient.GetStringAsync(userSuppliedUrl, cancellationToken);
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Ssrf_ignores_a_fixed_string_argument()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;

            class C
            {
                private readonly HttpClient httpClient = new();

                public void M() => httpClient.GetStringAsync("https://example.com/fixed");
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Ssrf_ignores_an_application_interface_method_named_like_an_http_verb()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Threading.Tasks;

            public interface IDocumentRepository
            {
                Task<string> GetAsync(long requisitionId, long documentId);
            }

            public sealed class GetDocumentQuery
            {
                private readonly IDocumentRepository repository;

                public GetDocumentQuery(IDocumentRepository repository)
                {
                    this.repository = repository;
                }

                public async Task<string> QueryAsync(long requisitionId, long documentId) =>
                    await repository.GetAsync(requisitionId, documentId);
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Ssrf_ignores_a_request_object_built_from_a_guid_and_a_fixed_path()
    {
        var ctx = MatchFixtures.Context(
            """
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                private readonly HttpClient httpClient = new();

                public async Task<int> SendAsync(Guid tenantId, CancellationToken cancellationToken)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tenants/{tenantId:D}/invitations");
                    using var response = await httpClient.SendAsync(request, cancellationToken);
                    return (int)response.StatusCode;
                }
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void Ssrf_traces_taint_through_a_private_forwarding_helper()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;
            using System.Threading.Tasks;

            class C
            {
                private readonly HttpClient httpClient = new();

                public Task<string> UnsafeAsync(string userPath) => GetAsync(userPath);

                private Task<string> GetAsync(string relativePath) => httpClient.GetStringAsync(relativePath);
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }

    [Fact]
    public void Ssrf_ignores_a_private_forwarding_helper_fed_only_by_safe_values()
    {
        var ctx = MatchFixtures.Context(
            """
            using System;
            using System.Net.Http;
            using System.Threading.Tasks;

            class C
            {
                private readonly HttpClient httpClient = new();

                public Task<string> GetTenantAsync(Guid tenantId) => GetAsync($"tenants/{tenantId:D}");

                private Task<string> GetAsync(string relativePath) => httpClient.GetStringAsync(relativePath);
            }
            """);
        SsrfPatterns.Ssrf(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SsrfTraversal_ignores_a_guid_formatted_interpolation_hole()
    {
        var ctx = MatchFixtures.Context(
            """
            using System;
            using System.Net.Http;
            using System.Threading;
            using System.Threading.Tasks;

            class C
            {
                private readonly HttpClient httpClient = new();

                public async Task<int> SendAsync(Guid tenantId, CancellationToken cancellationToken)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, $"api/tenants/{tenantId:D}/invitations");
                    using var response = await httpClient.SendAsync(request, cancellationToken);
                    return (int)response.StatusCode;
                }
            }
            """);
        SsrfPatterns.SsrfTraversal(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SsrfTraversal_ignores_a_whole_path_that_is_a_compile_time_constant()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;

            static class ResendConstants
            {
                public const string Emails = "emails";
            }

            class C
            {
                private readonly HttpClient httpClient = new();

                public void M()
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, ResendConstants.Emails);
                }
            }
            """);
        SsrfPatterns.SsrfTraversal(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SsrfTraversal_ignores_a_literal_path_plus_a_const_api_version()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;

            class C
            {
                private const string ApiVersion = "2024-01-01";

                private readonly HttpClient httpClient = new();

                public void M(string deployment)
                {
                    httpClient.GetStringAsync(
                        $"documentintelligence/documentModels/prebuilt-layout:analyze?api-version={ApiVersion}");
                }
            }
            """);
        SsrfPatterns.SsrfTraversal(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SsrfTraversal_ignores_a_configuration_value_wrapped_in_escape_data_string()
    {
        var ctx = MatchFixtures.Context(
            """
            using System;
            using System.Net.Http;

            class C
            {
                private const string ApiVersion = "2024-01-01";

                private readonly HttpClient httpClient = new();

                public void M(string deployment)
                {
                    httpClient.GetStringAsync(
                        $"openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={ApiVersion}");
                }
            }
            """);
        SsrfPatterns.SsrfTraversal(ctx, MatchFixtures.Id);
        Assert.Empty(ctx.Findings);
    }

    [Fact]
    public void SsrfTraversal_flags_an_unescaped_tainted_hole_in_the_path()
    {
        var ctx = MatchFixtures.Context(
            """
            using System.Net.Http;

            class C
            {
                private readonly HttpClient httpClient = new();

                public void M(string id) => httpClient.GetStringAsync($"items/{id}");
            }
            """);
        SsrfPatterns.SsrfTraversal(ctx, MatchFixtures.Id);
        Assert.Single(ctx.Findings);
    }
}
