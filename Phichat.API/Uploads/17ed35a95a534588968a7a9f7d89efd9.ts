import axios from 'axios'
import { getToken } from '../utils/jwt'

export const API = axios.create({
  baseURL: 'https://localhost:7146/api'
})

API.interceptors.request.use(config => {
  const token = getToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// پیام‌ها
export async function getConversationWith(userId: string) {
  const res = await API.get(`/messages/with/${userId}`)
  return res.data
}

export async function getMyMessages() {
  const res = await API.get('/messages')
  return res.data
}

// کلید چت
export async function getChatKey(userId: string): Promise<string | null> {
  try {
    const res = await API.get(`/keys/${userId}`)
    return res.data
  } catch (err: any) {
    if (err.response?.status === 404) return null
    throw err
  }
}

export async function storeChatKey(data: { receiverId: string; key: string }) {
  return await API.post('/keys', {
    receiverId: data.receiverId,
    encryptedKey: data.key // سرور هنوز اسم پارامتر رو encryptedKey می‌خواد
  })
}

// کاربران
export async function getUserList() {
  const res = await API.get('/users/list')
  return res.data
}

export async function getUserById(userId: string) {
  const res = await API.get(`/users/${userId}`)
  return res.data
}

export async function uploadEncryptedFile(formData: FormData): Promise<string> {
  const res = await API.post('/messages/upload', formData)
  return res.data.url
}

