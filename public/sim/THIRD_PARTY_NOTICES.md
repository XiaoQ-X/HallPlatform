# Third-Party Components

This Unity application includes the following open-source components. Their original license files are retained under Assets/ThirdParty and reproduced in the release licenses directory.

| Component | Version (commit) | License | Use |
| --- | --- | --- | --- |
| [Unity UI Extensions](https://github.com/Unity-UI-Extensions/com.unity.uiextensions) | d47f838ba918bbb9b26f242bd944940a711f525f | BSD-3-Clause | UICircle, UIPrimitiveBase and SetPropertyUtility: terminal rings, sampling progress and status indicators |
| [Unity UI Rounded Corners](https://github.com/kirevdokimov/Unity-UI-Rounded-Corners) | 33135342e868201eb0d4cef821ecab90caca373e | MIT | Antialiased input, button and dialog edges; upstream shaders and components |
| [Lucide](https://github.com/lucide-icons/lucide) | d9f72b25256fc57fabf9bfac0972fb97aa84aedd | ISC; MIT for listed Feather-derived icons | Navigation and tool icons; official SVGs rendered to PNG without changing shapes |

Licenses: Assets/ThirdParty/UIExtensions/LICENSE.txt, Assets/ThirdParty/UiRoundedCorners/LICENSE.txt, Assets/ThirdParty/Lucide/LICENSE.txt.

Upstream source files are vendored unchanged. Project integration is in Assets/THH/Runtime/WorkbenchUIStyle.cs. The icon renderer is a build-time tool only; generated PNG assets are committed and require no network access at runtime. Unity uGUI remains the base UI framework.

The license texts were compared with the pinned upstream revisions on 2026-09-18 (normalizing line endings and surrounding whitespace) and matched. The local audit is recorded in Docs/Rights-20260918/license-verification.json. This component notice does not license third-party equipment photographs, manuals, trademarks or product designs. Research originals are excluded from the teaching release. Unity engine and bundled Unity components remain subject to their applicable terms; system fonts are referenced at runtime rather than redistributed as font files.

## Selection

WebGL handoff adds Noto Sans SC Regular (Noto CJK, SIL Open Font License 1.1), redistributed at Assets/THH/Resources/Fonts/NotoSansSC-Regular.otf. Source: https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/SC . License: Assets/ThirdParty/NotoSansSC/LICENSE.txt. This bundled font provides Chinese glyphs in browsers; the earlier statement about system-only fonts applies to the original Windows release.

UnityNativeFilePicker was also reviewed (MIT), but it primarily targets Android/iOS and was not added to this Windows project. A directory input and explicit save operation avoid adding platform-specific native dependencies. The entire UI Extensions package was not imported; only the three required, dependency-complete source files are included.
