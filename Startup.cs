using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace dynamic_schema_based_on_header;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MatcherPolicy, HeaderRoutingMatcherPolicy>();
        services
            .AddGraphQLServer("first")
            .AddQueryType<Query>();
        services
            .AddGraphQLServer("second")
            .AddQueryType(x => x.Field("qux").Resolve("bar"));
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGraphQL(schemaName: "first")
                .WithMetadata(new HeaderMetadata("x-foo", "bar"));
            endpoints.MapGraphQL(schemaName: "second")
                .WithMetadata(new HeaderMetadata("x-foo", "baz"));
        });
    }
}

public class HeaderRoutingMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
{
    public override int Order => 0;

    public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
    {
        return endpoints.Any(e => e.Metadata.GetMetadata<HeaderMetadata>() is not null);
    }

    public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var metadata = candidates[i].Endpoint.Metadata.GetMetadata<HeaderMetadata>();
            var isMatch = metadata is { Name: var name, Value: var value } &&
                httpContext.Request.Headers[name].Any(x => x == value);

            candidates.SetValidity(i, isMatch);
        }

        return Task.CompletedTask;
    }
}

public record HeaderMetadata(string Name, string Value);
