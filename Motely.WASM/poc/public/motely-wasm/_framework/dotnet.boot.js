export const config = /*json-start*/{
  "mainAssemblyName": "Motely.WASM.dll",
  "resources": {
    "hash": "sha256-7SMvcjVYp/HHcjhgVodIPfdTZal5bqTrbAcNdxhHW4E=",
    "jsModuleWorker": [
      {
        "name": "dotnet.native.worker.mjs"
      }
    ],
    "jsModuleNative": [
      {
        "name": "dotnet.native.js"
      }
    ],
    "jsModuleRuntime": [
      {
        "name": "dotnet.runtime.js"
      }
    ],
    "wasmNative": [
      {
        "name": "dotnet.native.wasm",
        "integrity": "sha256-OswMFCQ5ORQwkiCtbDqhBaDJSbHka35qnDmU4/+UGrs="
      }
    ],
    "coreAssembly": [
      {
        "virtualPath": "DuckDB.NET.Bindings.wasm",
        "name": "DuckDB.NET.Bindings.wasm",
        "integrity": "sha256-wqxaaQM497E3IjjC7jdl4C41iuXUgt3CIC+L39iZ/lE="
      },
      {
        "virtualPath": "DuckDB.NET.Data.wasm",
        "name": "DuckDB.NET.Data.wasm",
        "integrity": "sha256-FP+b5XxswwHMDexG91pSwQBB4vpAgV2v1jF2BNkirLA="
      },
      {
        "virtualPath": "Motely.DB.wasm",
        "name": "Motely.DB.wasm",
        "integrity": "sha256-lYJNEqZvOsiJqufFqW9aHnaa4VKFQjUXpSxK87YB28s="
      },
      {
        "virtualPath": "Motely.wasm",
        "name": "Motely.wasm",
        "integrity": "sha256-hG+EUSXAcy7rXwN9qR2kvaFuHotZ+rbqLQ5Hg+BmGn8="
      },
      {
        "virtualPath": "Motely.Orchestration.wasm",
        "name": "Motely.Orchestration.wasm",
        "integrity": "sha256-9AddT36qp6pJeYQP0KwZ6HHsfg1bF8WMsJietOmJB1Q="
      },
      {
        "virtualPath": "Motely.WASM.wasm",
        "name": "Motely.WASM.wasm",
        "integrity": "sha256-MJHqbup03681i6zvpAkz5Wlk6YzzozwHKnJYyci/JXU="
      },
      {
        "virtualPath": "System.Collections.Concurrent.wasm",
        "name": "System.Collections.Concurrent.wasm",
        "integrity": "sha256-CWu7OvQpoj42ejCs1HpqnzisMWgXXobaaOjjtZQS7OQ="
      },
      {
        "virtualPath": "System.Collections.wasm",
        "name": "System.Collections.wasm",
        "integrity": "sha256-pQmhSB7BWqrKN53pAlqPyERgHOsjfLoAboW+KrJ+wwk="
      },
      {
        "virtualPath": "System.ComponentModel.Primitives.wasm",
        "name": "System.ComponentModel.Primitives.wasm",
        "integrity": "sha256-A4tZOdQlsq0vrR94Kgo+0cG+pd60w/RVYcnKOLj1xgE="
      },
      {
        "virtualPath": "System.ComponentModel.TypeConverter.wasm",
        "name": "System.ComponentModel.TypeConverter.wasm",
        "integrity": "sha256-R9CyKUM1Yc/xacERK46upAkz+nBtTnFvWidNXqgBsl8="
      },
      {
        "virtualPath": "System.Console.wasm",
        "name": "System.Console.wasm",
        "integrity": "sha256-hVfKWbmDIsHQtndXb6/U2hMhjh347IvXPg99nyWbKxU="
      },
      {
        "virtualPath": "System.Data.Common.wasm",
        "name": "System.Data.Common.wasm",
        "integrity": "sha256-oPkIp+I4fIjVPA9YDJ90yCc0asSTrmj0kkZ3c6aVdGw="
      },
      {
        "virtualPath": "System.wasm",
        "name": "System.wasm",
        "integrity": "sha256-YabWgbCEFJ/C33k/i+7FudaFKYcs5tDuAf9sQAhhC2A="
      },
      {
        "virtualPath": "System.IO.Pipelines.wasm",
        "name": "System.IO.Pipelines.wasm",
        "integrity": "sha256-UH3HJUXaHntZB5qAr8Kwhd2hYb7hh4McF4G11C92kbA="
      },
      {
        "virtualPath": "System.Linq.wasm",
        "name": "System.Linq.wasm",
        "integrity": "sha256-1RMGr3g1VE3AwchcXUa5tOIcO2OiP1aQ1vyO/ktGQg4="
      },
      {
        "virtualPath": "System.Linq.Expressions.wasm",
        "name": "System.Linq.Expressions.wasm",
        "integrity": "sha256-q2RyKG/0tx8nJ4QiKD4vTNYlLhrfSAtbHgxfHK0p5+Q="
      },
      {
        "virtualPath": "System.Memory.wasm",
        "name": "System.Memory.wasm",
        "integrity": "sha256-NXgsZkOt0tAgYGCTxrQZGbtOytOSYd1W2pAjm30wkxY="
      },
      {
        "virtualPath": "System.ObjectModel.wasm",
        "name": "System.ObjectModel.wasm",
        "integrity": "sha256-X18I4bR7MFPCBc/6qDmAvYmBRZqWKoycYhsKhacbEjc="
      },
      {
        "virtualPath": "System.Private.CoreLib.wasm",
        "name": "System.Private.CoreLib.wasm",
        "integrity": "sha256-81GFOgJ1o4C8Mn8SCZjDXxfqDQp9gT50wEg2awIznqM="
      },
      {
        "virtualPath": "System.Private.Uri.wasm",
        "name": "System.Private.Uri.wasm",
        "integrity": "sha256-TWTo87XjocKFIRkOBcm0RG7FbzwHSsxYSg9SlSPveXs="
      },
      {
        "virtualPath": "System.Runtime.InteropServices.JavaScript.wasm",
        "name": "System.Runtime.InteropServices.JavaScript.wasm",
        "integrity": "sha256-kMfuIHajm5NGI4GIVJI0/8O+C2k7ZW+oogCwkyMRpk8="
      },
      {
        "virtualPath": "System.Runtime.Numerics.wasm",
        "name": "System.Runtime.Numerics.wasm",
        "integrity": "sha256-gSb0Gu2r6r7VBStSEVMfeoKYpKIGZkGT+2c0QzmO5D8="
      },
      {
        "virtualPath": "System.Security.Cryptography.wasm",
        "name": "System.Security.Cryptography.wasm",
        "integrity": "sha256-Qty+ZNM0jlrG/c6Su5fMUvbr8z+9YsV+3QyaVxNI78s="
      },
      {
        "virtualPath": "System.Text.Encodings.Web.wasm",
        "name": "System.Text.Encodings.Web.wasm",
        "integrity": "sha256-4zQjsP8hUyhOj+/tb/ZNjxU5gummYmwzqSmHayXXhYQ="
      },
      {
        "virtualPath": "System.Text.Json.wasm",
        "name": "System.Text.Json.wasm",
        "integrity": "sha256-sdGrRV1ZiwO/kR8Tl5tK4iX3ojCyc7oXXP/PnJb/ilI="
      },
      {
        "virtualPath": "System.Text.RegularExpressions.wasm",
        "name": "System.Text.RegularExpressions.wasm",
        "integrity": "sha256-LivmUKsgqtW1rHzuKpfx+TjhO8yz/sJ2POGrenby/nQ="
      },
      {
        "virtualPath": "System.Threading.Channels.wasm",
        "name": "System.Threading.Channels.wasm",
        "integrity": "sha256-2EgxriwFFANyHLTavSVnZRryibA+Mmh+X5tpb753RzM="
      },
      {
        "virtualPath": "System.Threading.wasm",
        "name": "System.Threading.wasm",
        "integrity": "sha256-3OXHoIsSdpjSeLkN858NhrjDmrmTMx58i7XOujZ7EN8="
      },
      {
        "virtualPath": "YamlDotNet.wasm",
        "name": "YamlDotNet.wasm",
        "integrity": "sha256-Daw6C2JGNfcVFjWBMQMfBtMOVoH5M1f8TiJbAxdlOOA="
      },
      {
        "virtualPath": "aot-instances.wasm",
        "name": "aot-instances.wasm",
        "integrity": "sha256-ZKYH8qGQXmng3POhMUcorr9bMYF+uQANtiduldY9GFo="
      }
    ],
    "assembly": []
  },
  "debugLevel": 0,
  "globalizationMode": "invariant",
  "runtimeConfig": {
    "runtimeOptions": {
      "configProperties": {
        "Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability": true,
        "System.ComponentModel.DefaultValueAttribute.IsSupported": false,
        "System.ComponentModel.Design.IDesignerHost.IsSupported": false,
        "System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization": false,
        "System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported": false,
        "System.Data.DataSet.XmlSerializationIsSupported": false,
        "System.Diagnostics.Debugger.IsSupported": false,
        "System.Diagnostics.Metrics.Meter.IsSupported": false,
        "System.Diagnostics.Tracing.EventSource.IsSupported": false,
        "System.Globalization.Invariant": true,
        "System.TimeZoneInfo.Invariant": false,
        "System.Globalization.PredefinedCulturesOnly": true,
        "System.Linq.Enumerable.IsSizeOptimized": true,
        "System.Net.Http.EnableActivityPropagation": false,
        "System.Net.Http.WasmEnableStreamingResponse": true,
        "System.Net.SocketsHttpHandler.Http3Support": false,
        "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
        "System.Resources.ResourceManager.AllowCustomResourceTypes": false,
        "System.Resources.UseSystemResourceKeys": true,
        "System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported": true,
        "System.Runtime.InteropServices.BuiltInComInterop.IsSupported": false,
        "System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting": false,
        "System.Runtime.InteropServices.EnableCppCLIHostActivation": false,
        "System.Runtime.InteropServices.Marshalling.EnableGeneratedComInterfaceComImportInterop": false,
        "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false,
        "System.StartupHookProvider.IsSupported": false,
        "System.Text.Encoding.EnableUnsafeUTF7Encoding": false,
        "System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault": false,
        "System.Threading.Thread.EnableAutoreleasePool": false
      }
    }
  }
}/*json-end*/;