import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Versión en lote de get_element_by_id: lee varios ElementIds en una sola
// llamada. Agregada 2026-10-30 para auditorías con IDs ya conocidos (ej. una
// lista de 14 zapatas de un frente) sin necesitar una llamada por elemento.
export function registerGetElementsByIdsTool(server: McpServer) {
  server.tool(
    "get_elements_by_ids",
    "Batch version of get_element_by_id: read several Revit elements by their ElementIds in a single call, " +
      "including all instance and type parameters for each. Does not require any of them to be selected in the UI. " +
      "The response array has one entry per requested elementId, in the same order -- an entry is null if that ElementId doesn't exist in the active document.",
    {
      elementIds: z
        .array(z.number())
        .min(1)
        .describe("The Revit ElementIds (integers) of the elements to read"),
      includeTypeParameters: z
        .boolean()
        .optional()
        .describe(
          "Whether to also include parameters from each element's type (default: true)"
        ),
    },
    async (args, extra) => {
      const params = {
        elementIds: args.elementIds,
        includeTypeParameters:
          args.includeTypeParameters !== undefined
            ? args.includeTypeParameters
            : true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_elements_by_ids", params);
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
              text: `get elements by ids failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
