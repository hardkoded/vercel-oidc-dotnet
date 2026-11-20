// Copyright 2023 Vercel, Inc.
// Copyright 2026 Darío Kondratiuk
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>Lets <c>netstandard2.0</c> compile <c>init</c> accessors and records.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit;
#endif
