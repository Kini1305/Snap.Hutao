﻿// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Snap.Hutao.SourceGeneration.DependencyInjection;

/// <summary>
/// 注入HttpClient代码生成器
/// 旨在使用源生成器提高注入效率
/// 防止在运行时动态查找注入类型
/// </summary>
[Generator]
internal sealed class HttpClientGenerator : ISourceGenerator
{
    private const string DefaultName = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.HttpClientConfiguration.Default";
    private const string XRpcName = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.HttpClientConfiguration.XRpc";
    private const string XRpc2Name = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.HttpClientConfiguration.XRpc2";
    private const string XRpc3Name = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.HttpClientConfiguration.XRpc3";

    private const string PrimaryHttpMessageHandlerAttributeName = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.PrimaryHttpMessageHandlerAttribute";
    private const string DynamicSecretAttributeName = "Snap.Hutao.Web.Hoyolab.DynamicSecret.UseDynamicSecretAttribute";

    /// <inheritdoc/>
    public void Initialize(GeneratorInitializationContext context)
    {
        // Register a syntax receiver that will be created for each generation pass
        context.RegisterForSyntaxNotifications(() => new HttpClientSyntaxContextReceiver());
    }

    /// <inheritdoc/>
    public void Execute(GeneratorExecutionContext context)
    {
        // retrieve the populated receiver
        if (context.SyntaxContextReceiver is not HttpClientSyntaxContextReceiver receiver)
        {
            return;
        }

        StringBuilder sourceCodeBuilder = new();

        sourceCodeBuilder.Append($$"""
            // Copyright (c) DGP Studio. All rights reserved.
            // Licensed under the MIT license.

            using Snap.Hutao.Web.Hoyolab.DynamicSecret;
            using System.Net.Http;
            
            namespace Snap.Hutao.Core.DependencyInjection;
            
            internal static partial class IocHttpClientConfiguration
            {
                [global::System.CodeDom.Compiler.GeneratedCodeAttribute("{{nameof(HttpClientGenerator)}}","1.0.0.0")]
                [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
                public static partial IServiceCollection AddHttpClients(this IServiceCollection services)
                {
            """);

        FillWithHttpClients(receiver, sourceCodeBuilder);

        sourceCodeBuilder.Append("""

                    return services;
                }
            }
            """);

        context.AddSource("IocHttpClientConfiguration.g.cs", SourceText.From(sourceCodeBuilder.ToString(), Encoding.UTF8));
    }

    private static void FillWithHttpClients(HttpClientSyntaxContextReceiver receiver, StringBuilder sourceCodeBuilder)
    {
        List<string> lines = new();
        StringBuilder lineBuilder = new();

        foreach (INamedTypeSymbol classSymbol in receiver.Classes)
        {
            lineBuilder.Clear().Append("\r\n");
            lineBuilder.Append(@"        services.AddHttpClient<");

            AttributeData httpClientInfo = classSymbol
                .GetAttributes()
                .Single(attr => attr.AttributeClass!.ToDisplayString() == HttpClientSyntaxContextReceiver.AttributeName);
            ImmutableArray<TypedConstant> arguments = httpClientInfo.ConstructorArguments;

            if (arguments.Length == 2)
            {
                TypedConstant interfaceType = arguments[1];
                lineBuilder.Append($"{interfaceType.Value}, ");
            }

            TypedConstant configuration = arguments[0];

            lineBuilder.Append($"{classSymbol.ToDisplayString()}>(");

            string injectAsName = configuration.ToCSharpString();
            switch (injectAsName)
            {
                case DefaultName:
                    lineBuilder.Append("DefaultConfiguration)");
                    break;
                case XRpcName:
                    lineBuilder.Append("XRpcConfiguration)");
                    break;
                case XRpc2Name:
                    lineBuilder.Append("XRpc2Configuration)");
                    break;
                case XRpc3Name:
                    lineBuilder.Append("XRpc3Configuration)");
                    break;
                default:
                    throw new InvalidOperationException($"非法的 HttpClientConfiguration 值: [{injectAsName}]");
            }

            AttributeData? handlerInfo = classSymbol
                .GetAttributes()
                .SingleOrDefault(attr => attr.AttributeClass!.ToDisplayString() == PrimaryHttpMessageHandlerAttributeName);

            if (handlerInfo != null)
            {
                ImmutableArray<KeyValuePair<string, TypedConstant>> properties = handlerInfo.NamedArguments;
                lineBuilder.Append(@".ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler() {");

                foreach (KeyValuePair<string, TypedConstant> property in properties)
                {
                    lineBuilder.Append(' ');
                    lineBuilder.Append(property.Key);
                    lineBuilder.Append(" = ");
                    lineBuilder.Append(property.Value.ToCSharpString());
                    lineBuilder.Append(',');
                }

                lineBuilder.Append(" })");
            }

            if (classSymbol.GetAttributes().Any(attr => attr.AttributeClass!.ToDisplayString() == DynamicSecretAttributeName))
            {
                lineBuilder.Append(".AddHttpMessageHandler<DynamicSecretHandler>()");
            }

            lineBuilder.Append(';');

            lines.Add(lineBuilder.ToString());
        }

        foreach (string line in lines.OrderBy(x => x))
        {
            sourceCodeBuilder.Append(line);
        }
    }

    private class HttpClientSyntaxContextReceiver : ISyntaxContextReceiver
    {
        /// <summary>
        /// 注入特性的名称
        /// </summary>
        public const string AttributeName = "Snap.Hutao.Core.DependencyInjection.Annotation.HttpClient.HttpClientAttribute";

        /// <summary>
        /// 所有需要注入的类型符号
        /// </summary>
        public List<INamedTypeSymbol> Classes { get; } = new();

        /// <inheritdoc/>
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            // any class with at least one attribute is a candidate for injection generation
            if (context.Node is ClassDeclarationSyntax classDeclarationSyntax && classDeclarationSyntax.AttributeLists.Count > 0)
            {
                // get as named type symbol
                if (context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol)
                {
                    if (classSymbol.GetAttributes().Any(ad => ad.AttributeClass!.ToDisplayString() == AttributeName))
                    {
                        Classes.Add(classSymbol);
                    }
                }
            }
        }
    }
}