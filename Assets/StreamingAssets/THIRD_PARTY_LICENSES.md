# Third-Party Licenses

Gothic Unity is licensed under the GNU General Public License v3.0 (see `LICENSE` in the source repository:
https://github.com/Gothic-Unity-Project/Gothic-Unity).

No original Gothic game assets are bundled. All game content is loaded at runtime from the user's own
Gothic installation. Gothic is a trademark of THQ Nordic GmbH; this project is not affiliated with or endorsed by
THQ Nordic or Piranha Bytes.

This file lists third-party software that is shipped with (or required by) Gothic Unity builds, together with
its license. The original license files of the bundled binaries are kept next to them in
https://github.com/Gothic-Unity-Project/binary-dependencies (`Core/Dependencies/<Component>/`).
This file is placed inside `StreamingAssets/` so it is included in every build.


## Overview

| Component                 | Used for                                   | License                     |
|---------------------------|--------------------------------------------|-----------------------------|
| ZenKit / ZenKitCAPI       | Parsing Gothic assets (native)             | MIT                         |
| ZenKitCS                  | .NET bindings for ZenKit                   | MIT                         |
| libsquish                 | DXT texture decompression (inside ZenKit)  | MIT                         |
| mio                       | Memory-mapped file IO (inside ZenKit)      | MIT                         |
| dmusic / dmusic-cs        | DirectMusic reimplementation (music)       | MIT (Modern Variant)        |
| NVorbis                   | Ogg Vorbis decoding (mod dubbing)          | MIT                         |
| Mono I18N                 | Windows codepages (1250/1251/1252)         | MIT                         |
| Reflex                    | Dependency injection                       | MIT                         |
| UberLogger                | Logging                                    | MIT                         |
| MyBox                     | Unity editor/runtime utilities             | MIT                         |
| Newtonsoft JSON (Unity)   | JSON serialization                         | MIT / Unity Companion       |
| Liberation Sans           | Default TextMesh Pro font                  | SIL Open Font License 1.1   |
| Unity packages            | Engine, URP, XR, Localization, Inference   | Unity Companion License     |
| PICO OpenXR SDK           | PICO headset support                       | PICO SDK License            |
| HurricaneVR               | VR interaction framework                   | Unity Asset Store EULA      |


## MIT-licensed components

The following components are licensed under the MIT License. Copyright holders:

* **ZenKit** - Copyright (c) 2021-2024 GothicKit Contributors - https://github.com/GothicKit/ZenKit
* **ZenKitCAPI** - Copyright (c) 2023 GothicKit Contributors - https://github.com/GothicKit/ZenKitCAPI
* **ZenKitCS** - Copyright (c) 2023-2024 GothicKit Contributors - https://github.com/GothicKit/ZenKitCS
* **libsquish** - Copyright (c) 2006 Simon Brown - https://sourceforge.net/projects/libsquish/
* **mio** - Copyright (c) 2018 https://github.com/mandreyel/ - https://github.com/mandreyel/mio
* **Mono I18N** - Copyright (c) 2002 Southern Storm Software, Pty Ltd and Mono contributors -
  https://github.com/mono/mono/tree/main/mcs/class/I18N
* **Reflex** - Copyright (c) Gustavo P. Santos - https://github.com/gustavopsantos/reflex
* **UberLogger** - Copyright (c) 2015 bbbscarter - https://github.com/bbbscarter/UberLogger
* **MyBox** - Copyright (c) 2018 Andrew Rumak - https://github.com/Deadcows/MyBox
* **Newtonsoft JSON** - Copyright (c) 2007 James Newton-King - https://github.com/JamesNK/Newtonsoft.Json
  (Unity package wrapper: Copyright (c) 2022 Unity Technologies ApS, Unity Companion License)

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```


## dmusic / dmusic-cs

* Source: https://github.com/GothicKit/dmusic (native) and https://github.com/GothicKit/dmusic-cs (.NET bindings)
* License: MIT License (Modern Variant)

```
Copyright (c) 2024. GothicKit Contributors

Permission is hereby granted, without written agreement and without
license or royalty fees, to use, copy, modify, and distribute this
software and its documentation for any purpose, provided that the
above copyright notice and the following two paragraphs appear in
all copies of this software.

IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE TO ANY PARTY FOR
DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES
ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS DOCUMENTATION, EVEN
IF THE COPYRIGHT HOLDER HAS BEEN ADVISED OF THE POSSIBILITY OF SUCH
DAMAGE.

THE COPYRIGHT HOLDER SPECIFICALLY DISCLAIMS ANY WARRANTIES, INCLUDING,
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE.  THE SOFTWARE PROVIDED HEREUNDER IS
ON AN "AS IS" BASIS, AND THE COPYRIGHT HOLDER HAS NO OBLIGATION TO
PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
```


## NVorbis

* Version: 0.10.5
* Source: https://github.com/NVorbis/NVorbis
* Usage: Decodes Ogg Vorbis audio, which some mods ship as dubbing files (with a `.wav` extension).
  Vanilla Gothic 1/2 never uses it.
* Binary: `NVorbis.dll` from https://github.com/Gothic-Unity-Project/binary-dependencies (`Core/Dependencies/NVorbis/`)

```
MIT License

Copyright (c) 2020 Andrew Ward

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```


## Liberation Sans

Copyright (c) 2010 Google Corporation, Copyright (c) 2012 Red Hat, Inc.
Licensed under the SIL Open Font License, Version 1.1 - https://openfontlicense.org/


## Unity packages

Unity engine packages (Universal Render Pipeline, XR Management, OpenXR, XR Hands, Localization, Inference Engine,
uGUI, TextMesh Pro and others) are licensed under the Unity Companion License:
https://unity.com/legal/licenses/unity-companion-license


## PICO OpenXR SDK

Used for PICO headset builds. Licensed under the PICO SDK license by its respective owner:
https://developer.picoxr.com/


## HurricaneVR

Commercial VR interaction framework by CloudWalkin Games, licensed via the Unity Asset Store EULA:
https://unity.com/legal/as-terms
It is not part of the public source repository.


## Optional downloads (not shipped)

* **Whisper tiny** (speech-to-text model, optional, downloaded separately into `StreamingAssets/SpeechToText/`) -
  Copyright (c) OpenAI, Apache License 2.0 - https://huggingface.co/openai/whisper-tiny
