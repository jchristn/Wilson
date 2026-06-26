from __future__ import annotations

import json
from typing import Any
from urllib.parse import urlencode, quote
from urllib.request import Request, urlopen
from urllib.error import HTTPError


class WilsonClient:
    def __init__(self, base_url: str, token: str | None = None):
        self.base_url = base_url.rstrip("/")
        self.token = token

    def set_token(self, token: str | None) -> None:
        self.token = token

    def request(
        self,
        method: str,
        path: str,
        body: dict[str, Any] | None = None,
        query: dict[str, Any] | None = None,
    ) -> Any:
        url = self.base_url + path
        if query:
            filtered = {key: value for key, value in query.items() if value not in (None, "")}
            if filtered:
                url += "?" + urlencode(filtered)

        data = None if body is None else json.dumps(body).encode("utf-8")
        headers = {"Accept": "application/json"}
        if body is not None:
            headers["Content-Type"] = "application/json"
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"

        request = Request(url, data=data, headers=headers, method=method)
        try:
            with urlopen(request) as response:
                if response.status == 204:
                    return None
                return json.loads(response.read().decode("utf-8"))
        except HTTPError as exc:
            message = exc.read().decode("utf-8")
            raise RuntimeError(message or f"Wilson API request failed with HTTP {exc.code}") from exc

    def login(self, access_key: str) -> dict[str, Any]:
        result = self.request("POST", "/v1.0/auth/token", {"accessKey": access_key})
        self.token = result.get("token")
        return result

    def me(self) -> dict[str, Any]:
        return self.request("GET", "/v1.0/api/me")

    def model_runners(self, **params: Any) -> dict[str, Any]:
        return self.request("GET", "/v1.0/api/model-runners", query=params)

    def model_runner_health(self) -> list[dict[str, Any]]:
        return self.request("GET", "/v1.0/api/model-runners/health")

    def model_runner_health_by_id(self, runner_id: str) -> dict[str, Any]:
        return self.request("GET", f"/v1.0/api/model-runners/{quote(runner_id, safe='')}/health")

    def tools(self) -> list[dict[str, Any]]:
        return self.request("GET", "/v1.0/api/tools")

    def tool(self, name: str) -> dict[str, Any]:
        return self.request("GET", f"/v1.0/api/tools/{quote(name, safe='')}")

    def tool_run(self, run_id: str, **params: Any) -> dict[str, Any]:
        return self.request("GET", f"/v1.0/api/tool-runs/{quote(run_id, safe='')}", query=params)

    def conversation_tool_calls(self, conversation_id: str, **params: Any) -> dict[str, Any]:
        return self.request("GET", f"/v1.0/api/conversations/{quote(conversation_id, safe='')}/tool-calls", query=params)

    def request_history_tool_calls(self, request_history_id: str, **params: Any) -> dict[str, Any]:
        return self.request("GET", f"/v1.0/api/request-history/{quote(request_history_id, safe='')}/tool-calls", query=params)
