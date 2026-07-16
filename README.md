# Voice Dictate Demo

Android-only .NET MAUI sandbox **Register progress** (`WorkflowPage`) voice flow:

**Mic → live caption → stop → edit/confirm transcript → OpenAI (`gpt-4o-mini`) fills fields → review → OK**

Photo field is on the form to prove voice **ignores** it.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Visual Studio 2022/2026](https://visualstudio.microsoft.com/) with **.NET Multi-platform App UI** workload, **or** `maui-android` workload via CLI
- An **Android device or emulator** (API 26+)
- An **OpenAI API key**

Check workloads:

```powershell
dotnet workload install maui-android
```

---

## 1. Set your OpenAI API key

1. Create or edit [`Resources/Raw/openai.env`](Resources/Raw/openai.env) (copy from [`.env.example`](.env.example) if needed).
2. Set your key (no quotes):

```env
OPENAI_API_KEY=sk-your-real-key-here
OPENAI_MODEL=gpt-4o-mini
```

3. **Rebuild and redeploy** the app. The file is packaged into the APK at build time — editing it while the app is already installed is not enough.

> Prefer `openai.env` over `.env`. Android package assets with a leading dot are unreliable.

---

## 2. Restore and run (Android)

From this folder (`C:\Work\tests\voice-dictate-demo`):

```powershell
dotnet restore
dotnet build -t:Run -f net10.0-android
```

Or open `VoiceDictateDemo.csproj` in Visual Studio, select an Android target, and press **F5**.

Grant **microphone** (and speech recognition if prompted) when asked.

---

## 3. Try the demo

1. Tap **Mic** in the toolbar.
2. Speak, for example:  
   *"I've finished the job, worked two hours, replaced the pump and fixed the leak."*
3. Watch the live caption, then tap **Mic** again to stop.
4. Edit the transcript if needed → **Confirm**.
5. Progress status, hours, and activities should fill. Photo stays unchanged.
6. Adjust fields manually if you want, then tap **OK**.

Other buttons:

| Control | Action |
|---------|--------|
| **Cancel** (confirm panel) | Discard transcript |
| **Re-record** | Start listening again |
| **Close** | Demo close alert |
| Field rows | Manual pickers / editor |

---

## Architecture (single project)

| Piece | Role |
|-------|------|
| Community Toolkit `ISpeechToText` | On-device STT + live partial results |
| OpenAI Chat (`gpt-4o-mini`) | Structured field extraction after Confirm |
| `VoiceFieldSchemaBuilder` / `VoiceFieldApplier` |

No separate server process is required.

---

## Troubleshooting

| Issue | What to try |
|-------|-------------|
| `OPENAI_API_KEY` error on Confirm | Put the key in `Resources/Raw/openai.env`, then **rebuild/redeploy**. Dotfiles (`.env`) often fail to package on Android. |
| Mic does nothing | Check Android emulator mic settings; use a physical device if needed |
| Offline STT fails | App automatically retries with online OS STT (`SpeechToText.Default`). Status shows `Listening (Online (OS))…` |
| Build only Android | Project already targets `net10.0-android` only |

---

## Note

This is a **sandbox** for demos and UX/STT validation.
