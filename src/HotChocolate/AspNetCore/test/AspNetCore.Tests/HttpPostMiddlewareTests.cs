using System.Net;
using CookieCrumble;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using HotChocolate.AspNetCore.Instrumentation;
using HotChocolate.AspNetCore.Tests.Utilities;
using HotChocolate.Execution;
using Newtonsoft.Json;

namespace HotChocolate.AspNetCore;

public class HttpPostMiddlewareTests : ServerTestBase
{
    public HttpPostMiddlewareTests(TestServerFactory serverFactory)
        : base(serverFactory)
    {
    }

    [Fact]
    public async Task Simple_IsAlive_Test()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task LimitTokenCount_Success()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: s => s
                .AddGraphQL()
                .ModifyParserOptions(o => o.MaxAllowedNodes = 6));

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ s: __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task LimitTokenCount_Fail()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: s => s
                .AddGraphQLServer()
                .ModifyParserOptions(o => o.MaxAllowedNodes = 6));

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ s: __typename t: __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task MapGraphQLHttp_Simple_IsAlive_Test()
    {
        // arrange
        var server = CreateServer(endpoint => endpoint.MapGraphQLHttp());

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact(Skip = "We are currently reworking the query plans.")]
    public async Task Include_Query_Plan()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ __typename }" },
            includeQueryPlan: true);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Serialize_Payload_With_Whitespaces()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: sc => sc.AddHttpResponseFormatter(indented: true));

        // act
        var result = await server.PostRawAsync(
            new ClientQueryRequest { Query = "{ __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Serialize_Payload_Without_Extra_Whitespaces()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: sc => sc.AddHttpResponseFormatter(indented: false));

        // act
        var result = await server.PostRawAsync(
            new ClientQueryRequest { Query = "{ __typename }" });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Simple_IsAlive_Test_On_Non_GraphQL_Path()
    {
        // arrange
        var server = CreateStarWarsServer("/foo");

        // act
        var result = await server.PostAsync(
            new ClientQueryRequest { Query = "{ __typename }" },
            "/foo");

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_GetHeroName()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        hero {
                            name
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_GetHeroName_Casing_Is_Preserved()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        HERO: hero {
                            name
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Complexity_Exceeded()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: c => c.AddGraphQLServer().ModifyRequestOptions(o=>
            {
                o.Complexity.Enable = true;
                o.Complexity.MaximumAllowed = 1;
            }));

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        HERO: hero {
                            name
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_GetHeroName_With_EnumVariable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    query ($episode: Episode!) {
                        hero(episode: $episode) {
                            name
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "episode", "NEW_HOPE" }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_GetHumanName_With_StringVariable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    query h($id: String!) {
                        human(id: $id) {
                            name
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "id", "1000" }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Defer_Results()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostRawAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        ... @defer {
                            wait(m: 300)
                        }
                        hero(episode: NEW_HOPE)
                        {
                            name
                            ... on Droid @defer(label: ""my_id"")
                            {
                                id
                            }
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Single_Diagnostic_Listener_Is_Triggered()
    {
        // arrange
        var listenerA = new TestListener();

        var server = CreateStarWarsServer(
            configureServices: s => s
                .AddGraphQLServer()
                    .AddDiagnosticEventListener(_ => listenerA));

        // act
        await server.PostRawAsync(new ClientQueryRequest
        {
            Query = @"
                {
                    ... @defer {
                        wait(m: 300)
                    }
                    hero(episode: NEW_HOPE)
                    {
                        name
                        ... on Droid @defer(label: ""my_id"")
                        {
                            id
                        }
                    }
                }"
        });

        // assert
        Assert.True(listenerA.Triggered);
    }

    [Fact]
    public async Task Aggregate_Diagnostic_All_Listeners_Are_Triggered()
    {
        // arrange
        var listenerA = new TestListener();
        var listenerB = new TestListener();

        var server = CreateStarWarsServer(
            configureServices: s => s
                .AddGraphQLServer()
                    .AddDiagnosticEventListener(_ => listenerA)
                    .AddDiagnosticEventListener(_ => listenerB));

        // act
        await server.PostRawAsync(new ClientQueryRequest
        {
            Query = @"
                {
                    ... @defer {
                        wait(m: 300)
                    }
                    hero(episode: NEW_HOPE)
                    {
                        name
                        ... on Droid @defer(label: ""my_id"")
                        {
                            id
                        }
                    }
                }"
        });

        // assert
        Assert.True(listenerA.Triggered);
        Assert.True(listenerB.Triggered);
    }

    [Fact]
    public async Task Ensure_Multipart_Format_Is_Correct_With_Defer()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostRawAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        ... @defer {
                            wait(m: 300)
                        }
                        hero(episode: NEW_HOPE)
                        {
                            name
                            ... on Droid @defer(label: ""my_id"")
                            {
                                id
                            }
                        }
                    }"
            });

        // assert
        result.Content.MatchSnapshot();
    }

    [Fact]
    public async Task Ensure_Multipart_Format_Is_Correct_With_Defer_If_Condition_True()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostRawAsync(new ClientQueryRequest
            {
                Query = @"
                    query ($if: Boolean!){
                        ... @defer {
                            wait(m: 300)
                        }
                        hero(episode: NEW_HOPE)
                        {
                            name
                            ... on Droid @defer(label: ""my_id"", if: $if)
                            {
                                id
                            }
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    ["if"] = true
                }
            });

        // assert
        result.Content.MatchSnapshot();
    }

    [Fact]
    public async Task Ensure_JSON_Format_Is_Correct_With_Defer_If_Condition_False()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostRawAsync(new ClientQueryRequest
            {
                Query = @"
                    query ($if: Boolean!){
                        hero(episode: NEW_HOPE)
                        {
                            name
                            ... on Droid @defer(label: ""my_id"", if: $if)
                            {
                                id
                            }
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    ["if"] = false
                }
            });

        // assert
        result.Content.MatchSnapshot();
    }

    [Fact]
    public async Task Ensure_Multipart_Format_Is_Correct_With_Stream()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostRawAsync(
            new ClientQueryRequest
            {
                Query = @"
                    {
                        ... @defer {
                            wait(m: 300)
                        }
                        hero(episode: NEW_HOPE)
                        {
                            name
                            friends {
                                nodes @stream(initialCount: 1 label: ""foo"") {
                                    name
                                }
                            }
                        }
                    }"
            });

        // assert
        result.Content.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_CreateReviewForEpisode_With_ObjectVariable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    mutation CreateReviewForEpisode(
                        $ep: Episode!
                        $review: ReviewInput!) {
                        createReview(episode: $ep, review: $review) {
                            stars
                            commentary
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "ep", "EMPIRE" },
                    {
                        "review",
                        new Dictionary<string, object>
                        {
                            { "stars", 5 }, { "commentary", "This is a great movie!" },
                        }
                    }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_CreateReviewForEpisode_Omit_NonNull_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    mutation CreateReviewForEpisode(
                        $ep: Episode!
                        $review: ReviewInput!) {
                        createReview(episode: $ep, review: $review) {
                            stars
                            commentary
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    {
                        "review",
                        new Dictionary<string, object?>
                        {
                            { "stars", 5 },
                            { "commentary", "This is a great movie!" },
                        }
                    }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_CreateReviewForEpisode_Variables_In_ObjectValue()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    mutation CreateReviewForEpisode(
                        $ep: Episode!
                        $stars: Int!
                        $commentary: String!) {
                        createReview(episode: $ep, review: {
                            stars: $stars
                            commentary: $commentary
                        } ) {
                            stars
                            commentary
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "ep", "EMPIRE" },
                    { "stars", 5 },
                    { "commentary", "This is a great movie!" }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_CreateReviewForEpisode_Variables_Unused()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    mutation CreateReviewForEpisode(
                        $ep: Episode!
                        $stars: Int!
                        $commentary: String!
                        $foo: Float) {
                        createReview(episode: $ep, review: {
                            stars: $stars
                            commentary: $commentary
                        } ) {
                            stars
                            commentary
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "ep", "EMPIRE" },
                    { "stars", 5 },
                    { "commentary", "This is a great movie!" }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [InlineData("a")]
    [InlineData("b")]
    [Theory]
    public async Task SingleRequest_Execute_Specific_Operation(
        string operationName)
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    query a {
                        a: hero {
                            name
                        }
                    }

                    query b {
                        b: hero {
                            name
                        }
                    }",
                OperationName = operationName
            });

        // assert
        result.MatchSnapshot(operationName);
    }

    [Fact]
    public async Task SingleRequest_ValidationError()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        hero(episode: $episode) {
                            name
                        }
                    }",
                Variables = new Dictionary<string, object?>
                {
                    { "episode", "NEW_HOPE" }
                }
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_SyntaxError()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        ähero {
                            name
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Double_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query ($d: Float) {
                                 double_arg(d: $d)
                            }",
                    Variables = new Dictionary<string, object?>
                    {
                        { "d", 1.539 }
                    }
                },
                "/arguments");

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Double_Max_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query ($d: Float) {
                                 double_arg(d: $d)
                            }",
                    Variables = new Dictionary<string, object?>
                    {
                        { "d", double.MaxValue }
                    }
                },
                "/arguments");

        // assert
        new { double.MaxValue, result }.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Double_Min_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                        query ($d: Float) {
                             double_arg(d: $d)
                        }",
                Variables = new Dictionary<string, object?>
                {
                    { "d", double.MinValue }
                }
            },
                "/arguments");

        // assert
        new { double.MinValue, result }.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Decimal_Max_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query ($d: Decimal) {
                                 decimal_arg(d: $d)
                            }",
                    Variables = new Dictionary<string, object?>
                    {
                        { "d", decimal.MaxValue }
                    }
                },
                "/arguments");

        // assert
        new { decimal.MaxValue, result }.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Decimal_Min_Variable()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query ($d: Decimal) {
                                 decimal_arg(d: $d)
                            }",
                    Variables = new Dictionary<string, object?>
                    {
                        { "d", decimal.MinValue }
                    }
                },
                "/arguments");

        // assert
        new { decimal.MinValue, result }.MatchSnapshot();
    }

    [Fact]
    public async Task SingleRequest_Incomplete()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync("{ \"query\":    ");

        // assert
        result.MatchSnapshot();
    }

    [InlineData("{}", 1)]
    [InlineData("{ }", 2)]
    [InlineData("{\n}", 3)]
    [InlineData("{\r\n}", 4)]
    [Theory]
    public async Task SingleRequest_Empty(string request, int id)
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync(request);

        // assert
        result.MatchSnapshot(id);
    }

    [InlineData("[]", 1)]
    [InlineData("[ ]", 2)]
    [InlineData("[\n]", 3)]
    [InlineData("[\r\n]", 4)]
    [Theory]
    public async Task BatchRequest_Empty(string request, int id)
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync(request);

        // assert
        result.MatchSnapshot(id);
    }

    [Fact]
    public async Task EmptyRequest()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result = await server.PostAsync(string.Empty);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Ensure_Middleware_Mapping()
    {
        // arrange
        var server = CreateStarWarsServer("/foo");

        // act
        var result = await server.PostAsync(string.Empty);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task BatchRequest_GetHero_And_GetHuman()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostAsync(
                new List<ClientQueryRequest>
                {
                    new ClientQueryRequest
                    {
                        Query = @"
                            query getHero {
                                hero(episode: EMPIRE) {
                                    id @export
                                }
                            }"
                    },
                    new ClientQueryRequest
                    {
                        Query = @"
                            query getHuman {
                                human(id: $id) {
                                    name
                                }
                            }"
                    }
                });

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task BatchRequest_GetHero_And_GetHuman_MultiPart()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: sp => sp.AddHttpResponseFormatter());

        // act
        var response =
            await server.SendPostRequestAsync(
                JsonConvert.SerializeObject(new List<ClientQueryRequest>
                {
                    new ClientQueryRequest
                    {
                        Query = @"
                            query getHero {
                                hero(episode: EMPIRE) {
                                    id @export
                                }
                            }"
                    },
                    new ClientQueryRequest
                    {
                        Query = @"
                            query getHuman {
                                human(id: $id) {
                                    name
                                }
                            }"
                    }
                }),
                "/graphql");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new Exception("GraphQL endpoint not found.");
        }

        var result = await response.Content.ReadAsStringAsync();

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task OperationBatchRequest_GetHero_And_GetHuman()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostOperationAsync(
                new ClientQueryRequest
                {
                    Query =
                        @"query getHero {
                            hero(episode: EMPIRE) {
                                id @export
                            }
                        }

                        query getHuman {
                            human(id: $id) {
                                name
                            }
                        }"
                },
                "getHero, getHuman");

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task OperationBatchRequest_Invalid_BatchingParameter_1()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostOperationAsync(
                new ClientQueryRequest
                {
                    Query =
                        @"
                        query getHero {
                            hero(episode: EMPIRE) {
                                id @export
                            }
                        }

                        query getHuman {
                            human(id: $id) {
                                name
                            }
                        }"
                },
                "getHero",
                createOperationParameter: s => "batchOperations=" + s);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task OperationBatchRequest_Invalid_BatchingParameter_2()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostOperationAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query getHero {
                                hero(episode: EMPIRE) {
                                    id @export
                                }
                            }

                            query getHuman {
                                human(id: $id) {
                                    name
                                }
                            }"
                },
                "getHero, getHuman",
                createOperationParameter: s => "batchOperations=[" + s);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task OperationBatchRequest_Invalid_BatchingParameter_3()
    {
        // arrange
        var server = CreateStarWarsServer();

        // act
        var result =
            await server.PostOperationAsync(
                new ClientQueryRequest
                {
                    Query = @"
                            query getHero {
                                hero(episode: EMPIRE) {
                                    id @export
                                }
                            }

                            query getHuman {
                                human(id: $id) {
                                    name
                                }
                            }"
                },
                "getHero, getHuman",
                createOperationParameter: s => "batchOperations=" + s);

        // assert
        result.MatchSnapshot();
    }

    [Fact]
    public async Task Throw_Custom_GraphQL_Error()
    {
        // arrange
        var server = CreateStarWarsServer(
            configureServices: s => s.AddGraphQLServer()
                .AddHttpRequestInterceptor<ErrorRequestInterceptor>());

        // act
        var result =
            await server.PostAsync(new ClientQueryRequest
            {
                Query = @"
                    {
                        hero {
                            name
                        }
                    }"
            });

        // assert
        result.MatchSnapshot();
    }

    public class ErrorRequestInterceptor : DefaultHttpRequestInterceptor
    {
        public override ValueTask OnCreateAsync(
            HttpContext context,
            IRequestExecutor requestExecutor,
            IQueryRequestBuilder requestBuilder,
            CancellationToken cancellationToken)
        {
            throw new GraphQLException("MyCustomError");
        }
    }

    public class TestListener : ServerDiagnosticEventListener
    {
        public bool Triggered { get; set; }

        public override IDisposable ExecuteHttpRequest(HttpContext context, HttpRequestKind kind)
        {
            Triggered = true;
            return EmptyScope;
        }
    }
}
