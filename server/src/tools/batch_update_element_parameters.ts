import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Versión en lote de update_element_parameter: escribe varios (elemento,
// parámetro, valor) en UNA SOLA transacción de Revit, todo o nada. Agregada
// 2026-10-30 tras escribir 24 parámetros (12 elementos x 2 campos) con 24
// llamadas individuales, cada una en su propia transacción -- un fallo a
// medio camino habría dejado algunos elementos actualizados y otros no, sin
// forma automática de revertir los ya escritos.
export function registerBatchUpdateElementParametersTool(server: McpServer) {
  const updateSchema = z.object({
    elementId: z.number().describe("The Revit ElementId (integer) to write to"),
    parameterName: z
      .string()
      .describe(
        "Exact parameter name as returned by get_element_by_id/get_elements_by_ids, WITHOUT the 'Tipo: ' prefix even when writing at type level"
      ),
    value: z
      .string()
      .describe("New value as text, parsed to the parameter's real storage type server-side"),
    level: z
      .enum(["instance", "type", "auto"])
      .optional()
      .describe("Default: 'instance'. Same semantics as update_element_parameter's level."),
    unit: z
      .string()
      .optional()
      .describe(
        "Required per item when that parameter is a Double with a real unit spec (Length/Area/Volume). Same semantics as update_element_parameter's unit (m, cm, mm, ft, in, m2, ft2, m3, ft3...). Ignored for text/integer/unitless items."
      ),
  });

  server.tool(
    "batch_update_element_parameters",
    "Write several (elementId, parameterName, value) parameter updates in ONE Revit transaction -- all or nothing. " +
      "If ANY update in the batch would fail (element not found, parameter not found at the requested level, read-only parameter), " +
      "the ENTIRE batch is aborted before writing anything, and every item's result explains why. " +
      "If all updates validate but writing one of them throws, the whole transaction is rolled back and nothing is left half-applied. " +
      "Prefer this over calling update_element_parameter in a loop whenever writing more than one or two parameters together. " +
      "Each item with a real-unit Double parameter (Length/Area/Volume) needs its own 'unit' field -- omitting it fails validation for that item (UNIDAD_REQUERIDA) before anything is written.",
    {
      updates: z.array(updateSchema).min(1).describe("The batch of parameter writes to apply together"),
    },
    async (args, extra) => {
      const params = { updates: args.updates };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "batch_update_element_parameters",
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
              text: `batch update element parameters failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
