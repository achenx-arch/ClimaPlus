import { solicitar } from "./apiClient";
import type { Favorito } from "../types/api";

// contrato-favoritos.md (requiere sesión).

export function listarFavoritos(signal?: AbortSignal): Promise<Favorito[]> {
  return solicitar<Favorito[]>("/api/favoritos", { signal });
}

export function obtenerFavorito(idFavorito: number, signal?: AbortSignal): Promise<Favorito> {
  return solicitar<Favorito>(`/api/favoritos/${idFavorito}`, { signal });
}

/** 201 con el favorito creado; 409 si ya existe. */
export function agregarFavorito(idUbicacion: number): Promise<Favorito> {
  return solicitar<Favorito>("/api/favoritos", { metodo: "POST", cuerpo: { idUbicacion } });
}

/** 204; 404 si no existe o es de otro usuario. */
export function eliminarFavorito(idFavorito: number): Promise<void> {
  return solicitar<void>(`/api/favoritos/${idFavorito}`, { metodo: "DELETE" });
}
