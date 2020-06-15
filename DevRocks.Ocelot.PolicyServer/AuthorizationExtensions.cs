using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ocelot.Authorisation;
using Ocelot.Errors;
using Ocelot.Middleware;

namespace DevRocks.Ocelot.PolicyServer
{
    public static class AuthorizationExtensions
    {
        public static async Task Authorize(this HttpContext context, Func<Task> next)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            if (IsOptionsHttpMethod(context) || !downstreamRoute.IsAuthenticated)
            {
                await next.Invoke();
                return;
            }

            if (!AuthorizeByScope(context))
            {
                return;
            }

            const string policyKey = "policy";
            if (downstreamRoute.RouteClaimsRequirement.TryGetValue(policyKey, out var policy)
                && !await AuthorizeByPolicyAsync(context, policy))
            {
                return;
            }

            if (downstreamRoute.RouteClaimsRequirement.All(x => x.Key == policyKey))
            {
                await next.Invoke();
                return;
            }

            if (!AuthorizeByClaims(context))
            {
                return;
            }

            await next.Invoke();
        }

        private static bool AuthorizeByClaims(HttpContext  context)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            var claimsAuthoriser = context.RequestServices.GetRequiredService<IClaimsAuthoriser>();

            var authorised = claimsAuthoriser.Authorise(context.User,
                downstreamRoute.RouteClaimsRequirement,
                context.Items.TemplatePlaceholderNameAndValues());

            if (authorised.IsError)
            {
                SetPipelineError(context, authorised.Errors);
                return false;
            }

            if (authorised.Data)
            {
                return true;
            }

            SetPipelineError(context,
                new UnauthorisedError(
                    $"{context.User.Identity.Name} is not authorised to access {downstreamRoute.UpstreamPathTemplate.OriginalValue}"));

            return false;
        }

        private static async Task<bool> AuthorizeByPolicyAsync(HttpContext  context, string policy)
        {
            var authorizationService = context.RequestServices.GetService<IAuthorizationService>();

            var result = await authorizationService.AuthorizeAsync(context.User, new object(), policy);
            if (result.Succeeded)
            {
                return true;
            }

            SetPipelineError(context, new UnauthorisedError($"Forbidden with policy {policy}"));
            return false;
        }

        private static bool AuthorizeByScope(HttpContext  context)
        {
            var downstreamRoute = context.Items.DownstreamRoute();
            var scopesAuthoriser = context.RequestServices.GetRequiredService<IScopesAuthoriser>();
            var authorised = scopesAuthoriser.Authorise(context.User,
                downstreamRoute.AuthenticationOptions.AllowedScopes);

            if (authorised.IsError)
            {
                SetPipelineError(context, authorised.Errors);
                return false;
            }

            if (authorised.Data)
            {
                return true;
            }

            SetPipelineError(context,
                new UnauthorisedError(
                    $"{context.User.Identity.Name} unable to access {downstreamRoute.UpstreamPathTemplate.OriginalValue}"));

            return false;
        }

        private static bool IsOptionsHttpMethod(HttpContext  context)
        {
            return context.Request.Method.ToUpper() == "OPTIONS";
        }

        private static void SetPipelineError(HttpContext  context, Error error)
        {
            context.Items.Errors().Add(error);
        }

        private static void SetPipelineError(HttpContext  context, IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                SetPipelineError(context, error);
            }
        }
    }
}