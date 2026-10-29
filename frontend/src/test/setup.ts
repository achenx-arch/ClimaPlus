import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { establecerToken } from "../services/apiClient";

afterEach(() => {
  cleanup();
  establecerToken(null);
  sessionStorage.clear();
  localStorage.clear();
  vi.unstubAllGlobals();
});
