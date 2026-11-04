export interface Module { id: string; name: string; entryUrl: string; isActive: boolean; moduleActive?: boolean }
export interface User { id: string; firstNames: string; paternalLastName: string; maternalLastName: string | null; googleEmail: string | null; microsoftEmail: string | null; isActive?: boolean }
