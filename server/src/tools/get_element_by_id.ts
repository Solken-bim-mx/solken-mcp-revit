import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Lee un elemento arbitrario de Revit por su ElementId, sin requerir que esté
// seleccionado en la UI. Complemento de get_selected_elements -- agregado
// para poder verificar automáticamente escrituras de parámetros hechas vía
// send_code_to_revit (revit_actualizar_parametros_elemento en Solken).
export function registerGetElementByIdTool(server: McpServer) {
  server.tool(
    "get_element_by_id",
    "Get a specific Revit element by its ElementId, including all instance and type parameters. Unlike get_selected_elements, this does not require the element to be selected in the Revit UI -- useful for verifying a write right after it happens.",
    {
      elementId: z
        .number()
        .describe("The Revit ElementId (integer) of the element to read"),
      includeTypeParameters: z
        .boolean()
        .optional()
        .describe(
          "Whether to also include parameters from the element's type (default: true)"
        ),
    },
    async (args, extra) => {
      const params = {
        elementId: args.elementId,
        includeTypeParameters:
          args.includeTypeParameters !== undefined
            ? args.includeTypeParameters
            : true,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand("get_element_by_id", params);
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
              text: `get element by id failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
