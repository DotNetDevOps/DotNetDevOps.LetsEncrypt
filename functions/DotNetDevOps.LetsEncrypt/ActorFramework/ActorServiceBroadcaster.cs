using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;



namespace DotNetDevOps.LetsEncrypt
{
    public class ActorServiceBroadcaster : IActorService, IActorProxy
    {
        public string Name { get; }

        private readonly IServiceScopeFactory serviceScopeFactory;

       
      
        public ActorServiceBroadcaster(string name, IServiceScopeFactory serviceScopeFactory)
        {
            Name = name;
            this.serviceScopeFactory = serviceScopeFactory;
        }
        public async Task ExecuteAsync<TInput, TOutput, TState>(IDurableEntityContext ctx)
        {
            using (var scope = serviceScopeFactory.CreateScope())
            {
                var entityType = GeneratedFunction.TypeMappings[ctx.EntityName];
                var actor = scope.ServiceProvider.GetRequiredService(entityType) as Actor<TState>;
                actor.Id = new EntityId(ctx.EntityName, ctx.Key);
                actor.Context = ctx;
                actor.State = ctx.GetState<TState>(() => Activator.CreateInstance<TState>());

                var method = entityType.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<OperationAttribute>()?.Operation == ctx.OperationName);

                var values = method.GetParameters().Select(k => CreateParameter<TInput>(ctx, k, scope)).ToArray();
                var result = method.Invoke(actor, values);

                if (result is Task<TOutput> resultTask)
                {
                    ctx.Return(await resultTask);
                }
                else if (result is Task task)
                {
                    await task;
                }


                ctx.SetState(actor.State);



            }
        }

        private object CreateParameter<TInput>(IDurableEntityContext ctx, ParameterInfo k, IServiceScope scope)
        {
            if (k.ParameterType == typeof(TInput))
                return ctx.GetInput<TInput>();


            if (k.ParameterType == typeof(ILogger))
            {
                var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger($"{Name}.{ctx.OperationName}");
                logger.BeginScope(new Dictionary<string, string> {
                    ["OperationName"] = ctx.OperationName
                });

                return logger;
            }
                    
                    return scope.ServiceProvider.GetService(k.ParameterType);
        }

        string GetMethodCallName(LambdaExpression expression)
        {
            var unary = (UnaryExpression)expression.Body;
            var methodCall = (MethodCallExpression)unary.Operand;
            var constant = (ConstantExpression)methodCall.Object;
            var memberInfo = (MemberInfo)constant.Value;

            return memberInfo.Name;
        }

        public Task CallAsync<T, TInput>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task>>> ex, TInput input)
        {
            return ctx.CallEntityAsync(new EntityId(Name, key), GetMethodCallName(ex), input);
        }

        public Task<TOut> CallAsync<T, TInput, TOut>(IDurableOrchestrationContext ctx, string key, Expression<Func<T, Func<TInput, Task<TOut>>>> ex, TInput input)
        {

            return ctx.CallEntityAsync<TOut>(new EntityId(Name, key), GetMethodCallName(ex), input);

        }


    }
}
