# Studue MCP server

Studue speaks [MCP](https://modelcontextprotocol.io), so an AI assistant (Claude, ChatGPT, …) can read
and write your assignments for you. Point it at `https://studue.ch/mcp`, give it your student ID and
your write token, and you can say things like:

> Here is the PDF syllabus of Analysis 2 — put every exercise sheet into Studue with the right due date.

> What is due before Friday?

Everything the assistant does happens **as you**: same modules, same permissions, same shared data as
in the web app. Assignments it creates are visible to everyone attending that module.

## Tools

| Tool | What it does |
| --- | --- |
| `list_my_modules` | Your modules this semester. The module codes it returns are the only ones the write tools accept. |
| `list_assignments` | Assignments of your modules, earliest due date first. Optional filters: `moduleCode`, `from`, `to`. Defaults to today onwards. |
| `get_assignment` | One assignment by id. |
| `get_schedule` | Your weekly lecture schedule — lets the assistant turn "until the next lecture" into a real date. |
| `create_assignment` | Creates one assignment. |
| `create_assignments` | Creates many at once (e.g. a whole semester plan). All or nothing: if one entry is invalid, nothing is written. |
| `update_assignment` | Changes an existing assignment. Only the fields you pass are touched. |
| `delete_assignment` | Deletes an assignment (soft delete, like in the web app). |

Dates are `yyyy-MM-dd`, times `HH:mm`. Leaving the time out means "that day, no specific time".

## Configuration

### 1. Get your student ID and write token

Sign in on [studue.ch](https://studue.ch) and open the mail Studue sends you. The link in it looks like:

```
https://studue.ch/?write_token=abc123…&student_id=tomavant3
```

Those two query parameters are exactly what the MCP client needs.

### 2. Add the server

Claude Code:

```bash
claude mcp add --transport http studue https://studue.ch/mcp \
  --header "Authorization: Bearer abc123…" \
  --header "X-Student-Id: tomavant3"
```

Or, for clients configured via JSON:

```json
{
  "mcpServers": {
    "studue": {
      "type": "http",
      "url": "https://studue.ch/mcp",
      "headers": {
        "Authorization": "Bearer abc123…",
        "X-Student-Id": "tomavant3"
      }
    }
  }
}
```

Both headers are required. Without them — or with a wrong token — every call gets `401` and a
`WWW-Authenticate: Bearer realm="Mcp"` header back.

### 3. Keep the token

The write token is the same secret that keeps you signed in on the website. Treat it like a password:
anyone who has it can create, edit and delete assignments in your modules under your name.

Pressing **"Log out on all devices"** in the settings rotates the token, which also kills the MCP
configuration — you have to re-add the server with the new token from the next sign-in mail.
