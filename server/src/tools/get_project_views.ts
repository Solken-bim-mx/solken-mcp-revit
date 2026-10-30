import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetProjectViewsTool(server: any) {
    server.tool(
        "get_project_views",
        "Lists views in the active Revit project, by default only quantification/schedule tables (ViewType.Schedule) -- the same content the user sees under 'Schedules/Quantities' in the Project Browser. Use this to count or enumerate schedule tables without asking the user to check the Project Browser manually. Optionally filter by view type ('schedule' default, 'sheet', 'plan', 'all') and/or by a partial, case-insensitive name match. Key schedules and Revit's internal material-takeoff schedules are excluded automatically so the count matches what the user sees as real, navigable content.",
        {
            data: z.object({
                viewTypeFilter: z
                    .enum(["schedule", "sheet", "plan", "all"])
                    .optional()
                    .describe("Type of view to list. 'schedule' = quantification/schedule tables (default). 'sheet' = sheets. 'plan' = floor/ceiling/engineering plans. 'all' = every view."),
                nameContains: z
                    .string()
                    .optional()
                    .describe("Optional partial, case-insensitive filter against the view/table name, e.g. 'CONTRATRABE'."),
                includeTemplates: z
                    .boolean()
                    .optional()
                    .describe("Whether to include view templates. Default: false."),
            }).describe("Query parameters for listing project views."),
        },
        async (args: any) => {
            const params = args;
            try {
                const response = await withRevitConnection(async (revitClient: any) => {
                    return await revitClient.sendCommand("get_project_views", params);
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
                            text: `Get project views failed: ${error instanceof Error ? error.message : String(error)}`,
                        },
                    ],
                };
            }
        }
    );
}
