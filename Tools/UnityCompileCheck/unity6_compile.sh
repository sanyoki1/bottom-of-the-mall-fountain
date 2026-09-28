#!/bin/bash
# Compile-check every script exactly as Unity 6000.4.2f1 would, on a Linux machine with no Unity license
# (cloud sessions): Unity's own C# compiler (Roslyn 4.3.1), its .NET Standard 2.1 profile and the real
# engine/editor module assemblies, all taken from the Linux editor archive. It builds uGUI from the
# com.unity.ugui package source, then Assembly-CSharp twice (editor and player defines) and
# Assembly-CSharp-Editor. Stricter than `dotnet build Tools/UnityCompileCheck` (Unity 2022.3 reference
# assemblies), but still a compile check: it runs nothing.
#
# One-time setup (4.1 GB download; the extract keeps ~2 GB):
#   curl -o Unity.tar.xz https://download.unity3d.com/download_unity/7a4c1aeef971/LinuxEditorInstaller/Unity-6000.4.2f1.tar.xz
#   mkdir unity && tar -xJf Unity.tar.xz -C unity --wildcards 'Editor/Data/Managed/*' 'Editor/Data/NetStandard/*' \
#       'Editor/Data/DotNetSdkRoslyn/*' 'Editor/Data/Resources/PackageManager/BuiltInPackages/com.unity.ugui/*'
# Needs a .NET SDK/runtime (6 or later) on PATH to run Unity's csc.dll.
# Usage: bash Tools/UnityCompileCheck/unity6_compile.sh <extracted unity dir> [out dir]
# Expected: "exit 0" and no errors for all five assemblies.
set -u
U=${1:?usage: unity6_compile.sh <extracted unity dir> [out dir]}/Editor/Data
PROJ=$(cd "$(dirname "$0")/../.." && pwd)
OUT=${2:-$PROJ/Tools/UnityCompileCheck/obj/unity6}
mkdir -p "$OUT/ed" "$OUT/pl"
CSC="dotnet $U/DotNetSdkRoslyn/csc.dll"
UGUI=$U/Resources/PackageManager/BuiltInPackages/com.unity.ugui

# ── defines (the ones that could matter; the project's scripts use none, uGUI uses a few) ──
VER="UNITY_6000_4_2 UNITY_6000_4 UNITY_6000"
for y in 5_3 5_4 5_5 5_6 2017_1 2017_2 2017_3 2017_4 2018_1 2018_2 2018_3 2018_4 2019_1 2019_2 2019_3 2019_4 2020_1 2020_2 2020_3 \
         2021_1 2021_2 2021_3 2022_1 2022_2 2022_3 2023_1 2023_2 2023_3 6000_0 6000_1 6000_2 6000_3 6000_4; do VER="$VER UNITY_${y}_OR_NEWER"; done
COMMON="$VER PLATFORM_ARCH_64 UNITY_64 UNITY_INCLUDE_TESTS ENABLE_AUDIO ENABLE_CACHING ENABLE_CLOTH ENABLE_MICROPHONE ENABLE_MULTIPLE_DISPLAYS
 ENABLE_PHYSICS ENABLE_TEXTURE_STREAMING ENABLE_VIRTUALTEXTURING ENABLE_LZMA ENABLE_UNITYEVENTS ENABLE_VR ENABLE_WEBCAM ENABLE_UNITYWEBREQUEST
 ENABLE_WWW ENABLE_CUSTOM_RENDER_TEXTURE ENABLE_DIRECTOR ENABLE_LOCALIZATION ENABLE_SPRITES ENABLE_TERRAIN ENABLE_TILEMAP ENABLE_TIMELINE
 ENABLE_LEGACY_INPUT_MANAGER TEXTCORE_1_0_OR_NEWER PLATFORM_STANDALONE_WIN PLATFORM_STANDALONE UNITY_STANDALONE_WIN UNITY_STANDALONE
 ENABLE_RUNTIME_GI ENABLE_MOVIES ENABLE_NETWORK ENABLE_MONO NET_STANDARD_2_0 NET_STANDARD NET_STANDARD_2_1 NETSTANDARD NETSTANDARD2_1
 ENABLE_PROFILER DEBUG TRACE UNITY_ASSERTIONS CSHARP_7_OR_LATER CSHARP_7_3_OR_NEWER"
EDITORDEF="UNITY_EDITOR UNITY_EDITOR_64 UNITY_EDITOR_WIN ENABLE_UNITY_COLLECTIONS_CHECKS"
UGUIDEF="PACKAGE_PHYSICS PACKAGE_PHYSICS2D PACKAGE_TILEMAP PACKAGE_ANIMATION PACKAGE_UITOOLKIT"
defs() { local d=""; for x in $@; do d="$d;$x"; done; echo "-define:${d#;}"; }

# ── references: .NET Standard 2.1 (Unity's apiCompatibilityLevel 6), engine modules, editor modules ──
NETREF="-r:$U/NetStandard/ref/2.1.0/netstandard.dll"
for f in "$U"/NetStandard/compat/2.1.0/shims/netfx/*.dll "$U"/NetStandard/compat/2.1.0/shims/netstandard/*.dll; do NETREF="$NETREF -r:$f"; done
ENGREF=""
for f in "$U"/Managed/UnityEngine/UnityEngine.dll "$U"/Managed/UnityEngine/UnityEngine.*Module.dll; do ENGREF="$ENGREF -r:$f"; done
EDREF="-r:$U/Managed/UnityEngine/UnityEditor.dll"
for f in "$U"/Managed/UnityEngine/UnityEditor.*Module.dll; do EDREF="$EDREF -r:$f"; done
# ProjectSettings: allowUnsafeCode 0, deterministic; Unity's default nowarns
OPTS="-target:library -nostdlib+ -noconfig -langversion:9.0 -deterministic -optimize- -debug:portable -nologo -warn:4 -utf8output
 -nowarn:0169 -nowarn:0649 -nowarn:0282 -nowarn:1701 -nowarn:1702"

fail=0
run() {
  local name=$1; shift
  echo "── $name"
  $CSC "$@" 2>&1 | grep -E "error|warning CS" | sed "s|$PROJ/||; s|$UGUI/||" | sort | uniq -c | sort -rn | head -60
  local code=${PIPESTATUS[0]}
  echo "   exit $code"
  [ "$code" = 0 ] || fail=1
}
list() { find "$@" -name "*.cs" | sed 's/.*/"&"/' | tr '\n' ' '; }

list "$UGUI/Runtime/UGUI" > "$OUT/ugui.rsp"
run "UnityEngine.UI (editor)" $OPTS $NETREF $ENGREF $EDREF "$(defs $COMMON $EDITORDEF $UGUIDEF)" -out:"$OUT/ed/UnityEngine.UI.dll" @"$OUT/ugui.rsp"
run "UnityEngine.UI (player)" $OPTS $NETREF $ENGREF "$(defs $COMMON $UGUIDEF)" -out:"$OUT/pl/UnityEngine.UI.dll" @"$OUT/ugui.rsp"

find "$PROJ/Assets" -name "*.cs" -not -path "*/Editor/*" | sed 's/.*/"&"/' | tr '\n' ' ' > "$OUT/game.rsp"
run "Assembly-CSharp (editor)" $OPTS $NETREF $ENGREF $EDREF -r:"$OUT/ed/UnityEngine.UI.dll" "$(defs $COMMON $EDITORDEF)" -out:"$OUT/ed/Assembly-CSharp.dll" @"$OUT/game.rsp"
# the player compile uses the editor's module assemblies (a superset of the player's); good enough to catch UNITY_EDITOR-only code
run "Assembly-CSharp (player)" $OPTS $NETREF $ENGREF -r:"$OUT/pl/UnityEngine.UI.dll" "$(defs $COMMON)" -out:"$OUT/pl/Assembly-CSharp.dll" @"$OUT/game.rsp"

find "$PROJ/Assets" -name "*.cs" -path "*/Editor/*" | sed 's/.*/"&"/' | tr '\n' ' ' > "$OUT/editor.rsp"
run "Assembly-CSharp-Editor" $OPTS $NETREF $ENGREF $EDREF -r:"$OUT/ed/UnityEngine.UI.dll" -r:"$OUT/ed/Assembly-CSharp.dll" "$(defs $COMMON $EDITORDEF)" -out:"$OUT/ed/Assembly-CSharp-Editor.dll" @"$OUT/editor.rsp"

[ $fail = 0 ] && echo "[unity6_compile] all assemblies compiled" || echo "[unity6_compile] FAILED"
exit $fail
