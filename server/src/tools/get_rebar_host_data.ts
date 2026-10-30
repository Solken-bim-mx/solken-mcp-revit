import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetRebarHostDataTool(server: any) {
    server.tool(
        "get_rebar_host_data",
        "Given one or more Revit element IDs of host elements (footings/zapatas, grade beams/contratrabes, columns/castillos, walls, slabs), returns the rebar bars Revit reports as actually placed inside each host, using Revit's real internal host relationship (RebarHostData) -- not bounding box proximity or category-wide dumps. Use this instead of ai_element_filter with OST_Rebar when you need to know exactly which rebar belongs to a specific foundation/beam element. If a host has no rebar data (not yet armed, or its category doesn't support hosted rebar), hasRebarHostData will be false for that host -- this is normal information, not an error.",
        {
            data: z.object({
                hostElementIds: z
                    .array(z.number())
                    .min(1)
                    .describe("Array of Revit ElementIds for the host elements to query (e.g. footing/zapata, grade beam/contratrabe, or column/castillo element IDs already known from a prior ai_element_filter call)."),
            }).describe("Query parameters for fetching rebar hosted inside specific elements."),
        },
        async (args: any) => {
            const params = args;
            try {
                const response = await withRevitConnection(async (revitClient: any) => {
                    return await revitClient.sendCommand("get_rebar_host_data", params);
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
                            text: `Get rebar host data failed: ${error instanceof Error ? error.message : String(error)}`,
                        },
                    ],
                };
            }
        }
    );
}
