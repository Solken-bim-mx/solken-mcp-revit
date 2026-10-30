import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";

export function registerGetScheduleDataTool(server: any) {
    server.tool(
        "get_schedule_data",
        "Extracts the headers and body rows of one or more quantification/schedule tables (ViewSchedule), given their ElementIds (obtained from get_project_views). Reads the table's actual content -- the same formatted text the user sees in Revit -- without needing the schedule to be the active view or the user to switch views manually. Use this after get_project_views has identified which schedule tables to read. If a schedule has no separate header section, headers will be an empty array for that table -- this is normal, not an error.",
        {
            data: z.object({
                scheduleViewIds: z
                    .array(z.number())
                    .min(1)
                    .describe("Array of ElementIds for the ViewSchedule(s) to read, as returned by get_project_views."),
                includeHeaders: z
                    .boolean()
                    .optional()
                    .describe("Whether to include the column header row. Default: true."),
                maxRows: z
                    .number()
                    .optional()
                    .describe("Optional cap on the number of body rows returned per table. Omit to return all rows."),
            }).describe("Query parameters for reading schedule table content."),
        },
        async (args: any) => {
            const params = args;
            try {
                const response = await withRevitConnection(async (revitClient: any) => {
                    return await revitClient.sendCommand("get_schedule_data", params);
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
                            text: `Get schedule data failed: ${error instanceof Error ? error.message : String(error)}`,
                        },
                    ],
                };
            }
        }
    );
}
