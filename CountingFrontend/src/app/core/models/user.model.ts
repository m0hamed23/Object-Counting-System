export interface User {
    id: number;
    username: string;
}
  
export interface UserCreate {
    username: string;
    password?: string;
}