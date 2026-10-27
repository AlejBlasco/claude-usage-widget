# SDLC Kit y servidores MCP

Guía para trabajar en este repositorio con el [SDLC Kit](https://github.com/AlejBlasco/claude-sdlc-kit) de Claude Code y los MCP del proyecto. Movida desde el README para mantenerlo centrado en el producto.

Esta sección explica cómo instalar y configurar el [SDLC Kit](https://github.com/AlejBlasco/claude-sdlc-kit) —el conjunto de subagentes y comandos `/sdlc-*` de Claude Code— para trabajar en este proyecto, y cómo dejar operativo el MCP de GitHub que la pipeline usa para issues y milestones.

### 1. Descargar el SDLC Kit

```bash
git clone https://github.com/AlejBlasco/claude-sdlc-kit.git
```

Copia la carpeta `.claude/` del kit en la raíz de este repositorio (haciendo merge con la que ya existe aquí, sin sobrescribir ficheros que no vengan del kit):

```
claude-usage-widget/
├── .claude/
│   ├── sdlc.config.yaml
│   ├── agents/            business-analyst, software-architect, software-developer,
│   │                       technical-writer, qa-engineer, devops-engineer
│   ├── commands/           sdlc-analysis, sdlc-design, sdlc-development,
│   │                       sdlc-documentation, sdlc-testing, sdlc-implementation
│   └── skills/             skills específicas de cada agente
└── .github/
    └── ISSUE_TEMPLATE/      feature_request.md, bug_report.md
```

Reinicia/recarga Claude Code en este repo para que recoja los nuevos agentes y comandos.

### 2. Configurar el kit

La configuración vive en [`.claude/sdlc.config.yaml`](../.claude/sdlc.config.yaml). En este proyecto está fijada así:

| Clave | Valor | Significado |
|---|---|---|
| `xmlDocComments` | `es` | Idioma de los comentarios de documentación en código (XMLDoc) generados por el Software Developer. |
| `documentation` | `es` | Idioma de toda la documentación markdown generada por la pipeline. |
| `issueTemplates` | `en` | Idioma en el que están escritas las plantillas de `.github/ISSUE_TEMPLATE/` (no la lee ningún agente en tiempo de ejecución, solo documenta el idioma usado). |
| `testingCoverage` | `70` | Cobertura mínima de test que persigue el QA Engineer. |
| `paths.*` | `docs/sdlc/...`, `docs/functional` | Carpetas de salida de cada fase (requirements, design, development, technicalDocs, functionalDocs, testing, deployment). |

**Invariante duro**: ningún agente/comando del kit ejecuta nunca `git commit` ni `git push` — eso es siempre una acción manual del usuario.

### 3. Usar la pipeline

Cada comando acepta una ruta a un `.md` de la fase anterior (encadenando la pipeline), una URL a un work item (issue de GitHub, Azure DevOps, Jira...), o una descripción en texto libre:

```
/sdlc-analysis Los usuarios deben poder configurar el intervalo de polling
/sdlc-design docs/sdlc/requirements/polling-interval.md
/sdlc-development docs/sdlc/design/polling-interval.md
/sdlc-documentation docs/sdlc/development/polling-interval.md
/sdlc-testing docs/sdlc/development/polling-interval.md
```

Flujo natural: **análisis → diseño → desarrollo → (documentación y testing, en cualquier orden) → implementación**. `sdlc-implementation` prepara/revisa artefactos de despliegue (Compose, CI/CD, runbooks); nunca despliega nada él mismo.

### 4. Ficheros MCP del proyecto

[`.mcp.json`](../.mcp.json), en la raíz del repo, declara los servidores MCP a nivel de proyecto — se versiona en git, así que cualquiera que abra este repo con Claude Code hereda la misma configuración automáticamente. No debe contener secretos en claro: los valores sensibles se referencian como `${VARIABLE_DE_ENTORNO}` y Claude Code los resuelve desde el entorno al arrancar.

### 5. Configurar el MCP de GitHub (issues y milestones)

El kit usa el MCP oficial y remoto de GitHub para crear/gestionar issues y milestones desde la pipeline (p. ej. al cerrar preguntas abiertas de `sdlc-analysis`). Configuración actual en `.mcp.json`:

```json
{
  "mcpServers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": {
        "Authorization": "Bearer ${GITHUB_PERSONAL_ACCESS_TOKEN}",
        "X-MCP-Toolsets": "issues"
      }
    }
  }
}
```

El toolset está restringido a `issues` (que en el servidor de GitHub incluye también la gestión de milestones) — nada de pull requests, actions, etc. de momento.

**Para dejarlo operativo necesitas un token propio:**

1. Ve a [github.com/settings/personal-access-tokens/new](https://github.com/settings/personal-access-tokens/new).
2. **Resource owner**: tu usuario/organización. **Repository access**: "Only select repositories" → este repositorio.
3. **Permissions → Repository permissions → Issues**: `Read and write` (cubre issues y milestones). El resto en "No access"; `Metadata` queda en `Read-only` de forma obligatoria.
4. Genera el token (`github_pat_...`) y cópialo — solo se muestra una vez.
5. Guárdalo como variable de entorno de usuario, por ejemplo en PowerShell:
   ```powershell
   [System.Environment]::SetEnvironmentVariable("GITHUB_PERSONAL_ACCESS_TOKEN", "github_pat_...", "User")
   ```
6. Reinicia la terminal/Claude Code para que recoja la variable (las de nivel "User" no se propagan a procesos ya abiertos).

El token nunca se escribe en ningún fichero de este repositorio — solo vive en la variable de entorno de tu máquina.

### 6. Configurar el MCP de Playwright (tests E2E del contenido Razor)

`ClaudeMeter.Desktop` no es una web app, pero `BlazorWebView` aloja WebView2 (Chromium embebido) — así que se puede automatizar/inspeccionar igual que una página web, conectando Playwright por CDP (Chrome DevTools Protocol) al proceso ya en marcha, en vez de dejar que Playwright lance su propio navegador. Configuración actual en `.mcp.json`:

```json
{
  "mcpServers": {
    "playwright": {
      "command": "npx",
      "args": ["-y", "@playwright/mcp@latest", "--cdp-endpoint=http://localhost:9222"]
    }
  }
}
```

**Para usarlo, arranca el widget con el puerto de depuración remota abierto:**

```powershell
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=9222"
dotnet run --project src/ClaudeMeter.Desktop
```

Con el proceso corriendo, el MCP de Playwright puede navegar/inspeccionar el DOM real dentro del `BlazorWebView` (verificar que las barras muestran el porcentaje/color correcto con CSS realmente aplicado por un motor Chromium, que el aviso de 401/403 se renderiza, etc.) — un nivel de verificación que bUnit no cubre porque renderiza sobre un DOM virtual, no un navegador real.

**Qué NO cubre esto:** el chrome de la ventana WPF (`WindowStyle=None`, `AllowsTransparency`, `Topmost`, arrastrar con el ratón, cerrar la ventana) vive fuera del `BlazorWebView` — Playwright no tiene visibilidad de eso. Esas partes siguen dependiendo de validación manual en un Windows real, tal como ya documentan las fases de testing de cada milestone.
