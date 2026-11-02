import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { withRevitConnection } from "../utils/ConnectionManager.js";

// Lista tipos de familia de una categoría con su conteo real de instancias
// colocadas, incluyendo 0. Agregada 2026-10-30: get_available_family_types
// (heredada del fork original) no distingue un tipo con instancias reales de
// uno huérfano (creado, nunca colocado) -- eso llevó a una cuenta equivocada
// de 28 "zapatas" en eje 6-7 cuando solo 14 tenían instancia real.
export function registerListFamilyTypesWithInstanceCountTool(server: McpServer) {
  server.tool(
    "list_family_types_with_instance_count",
    "List family types (FamilySymbol/ElementType) in a given Revit category, each with how many instances are actually placed in the model -- including 0. " +
      "A type with instanceCount 0 is an orphan: it exists in the project's type catalog but was never placed anywhere. " +
      "Use this instead of a plain type listing whenever you need to know what's REALLY built in the model, not just what type definitions exist. " +
      "categoryName must be a Revit BuiltInCategory name, e.g. 'OST_StructuralFoundation' (footings/grade beams), 'OST_Floors' (floors/slabs, also used for 'plantilla' templates in this project), 'OST_StructuralColumns', 'OST_Walls'.",
    {
      categoryName: z
        .string()
        .describe(
          "Revit BuiltInCategory name, e.g. 'OST_StructuralFoundation' or 'OST_Floors'"
        ),
      nameContains: z
        .string()
        .optional()
        .describe(
          "Optional case-insensitive filter against the family name or type name"
        ),
    },
    async (args, extra) => {
      const params = {
        categoryName: args.categoryName,
        nameContains: args.nameContains,
      };

      try {
        const response = await withRevitConnection(async (revitClient) => {
          return await revitClient.sendCommand(
            "list_family_types_with_instance_count",
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
              text: `list family types with instance count failed: ${
                error instanceof Error ? error.message : String(error)
              }`,
            },
          ],
        };
      }
    }
  );
}
