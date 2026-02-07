// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Minimal entry point: boot WASM runtime and run C# Main (keeps runtime alive for JSExport calls).
// Consumed via npm use loadMotely(); this file is for standalone serve of publish output only.

import { dotnet } from './_framework/dotnet.js';

const { runMain } = await dotnet.withApplicationArguments('start').create();
await runMain();
