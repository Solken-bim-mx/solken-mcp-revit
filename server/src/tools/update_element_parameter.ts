import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Escribe un único parámetro (instancia o tipo) en un elemento por su
// ElementId, con el nivel resuelto explícito en la respuesta. Agregado
// 2026-10-30: varias familias del proyecto tenían campos de gestión (Estado,
// Avance, etc.) atrapados a nivel Tipo -- la única herramienta de escritura
// existente en el stack de Solken solo sabía escribir instancia, y fallaba
// con NO_ENCONTRADO en esos casos. Esta herramienta cubre ambos niveles de
// forma nativa, sin recurrir a send_code_to_revit.
export function registerUpdateElementParameterTool(server: McpServer) {
  server.tool(
    "update_element_parameter",
    "Write a single instance or type parameter on a Revit element by its ElementId. " +
      "level='instance' (default) only writes if the parameter exists at the instance level -- fails with NO_ENCONTRADO otherwise. " +
      "level='type' writes to the element's type/family symbol -- this affects EVERY instance that shares that type, not just the one queried; the response's sharedInstanceCount says how many. " +
      "level='auto' tries instance first and falls back to type only if the parameter isn't found at instance level. " +
      "Before calling this, read the element with get_element_by_id first: if the parameter name you want appears as a bare key, it's instance-level; if it only appears prefixed 'Tipo: <name>', it's type-level and you must pass level='type' or 'auto' explicitly, or the call will fail with NO_ENCONTRADO. " +
      "Values are parsed according to the parameter's real StorageType (text, integer or number). " +
      "For Double parameters with a REAL unit spec (Length/Area/Volume...), you MUST pass 'unit' -- the tool converts value+unit to Revit's internal units before writing. " +
      "Unitless Double parameters (like Avance, a 0-1 ratio) need no 'unit' and ignore it if passed. " +
      "If 'unit' is required and missing, the call fails with UNIDAD_REQUERIDA instead of guessing.",
    {
      elementId: z
        .number()
        .describe("The Revit ElementId (integer) of the element to write to"),
      parameterName: z
        .string()
        .describe(
          "Exact parameter name as returned by get_element_by_id, WITHOUT the 'Tipo: ' prefix even when writing at type level"
        ),
      value: z
        .string()
        .describe(
          "New value as text. Parsed to the parameter's real storage type (string/integer/number) server-side."
        ),
      level: z
        .enum(["instance", "type", "auto"])
        .optional()
        .describe(
          "Where to look for and write the parameter. Default: 'instance'. Use 'auto' when unsure, or 'type' when get_element_by_id showed the field only as 'Tipo: <name>'."
        ),
      unit: z
        .string()
        .optional()
        .describe(
          "Required when the parameter is a Double with a real unit spec (Length/Area/Volume). One of: m, cm, mm, ft, in, m2, ft2, m3, ft3 (and a few aliases). " +
            "Ignored for text, integer, and unitless Double parameters (e.g. Avance). Omitting it for a parameter that needs it fails the call rather than writing a wrong value."
        ),
    },
    async (args, extra) => {
      const params = {
        elementId: args.elementId,
        parameterName: args.parameterName,
        value: args.value,
        level: args.level ?? "instance",
        unit: args.unit,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "update_element_parameter",
            params
          );
        });

        return {
          content: [
            {
              type: "text",
              text: JSON.stringify(response, null, 2),
            },
          ],
        };
      } catch (error) {
        return {
          content: [
            {
              type: "text",
              text: `update element parameter failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
