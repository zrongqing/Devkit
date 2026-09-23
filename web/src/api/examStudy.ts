import { authorizedFetch, request } from "./session";

export interface Owned {
  id: string;
  ownerId: string;
  revision: number;
  createdAtUtc: string;
}
export interface KnowledgeBase extends Owned {
  name: string;
  description: string;
  tags: string;
}
export interface Source extends Owned {
  knowledgeBaseId: string;
  title: string;
  fileId: string | null;
  kind: string;
  text: string;
  status: string;
  warning: string;
  publishedRevision: number;
}
export interface Chunk extends Owned {
  sourceId: string;
  versionId: string;
  knowledgeBaseId: string;
  ordinal: number;
  text: string;
  heading: string;
  location: string;
  page: number | null;
}
export interface SourceVersion extends Owned {
  sourceId: string;
  title: string;
  fileId: string | null;
  text: string;
}
export interface SourceDetail {
  source: Source;
  versions: SourceVersion[];
  chunks: Chunk[];
}
export interface Project extends Owned {
  name: string;
  description: string;
  targetDate: string | null;
  knowledgeBaseIdsJson: string;
}
export interface Citation {
  chunkId: string;
  sourceId: string;
  versionId: string;
  knowledgeBaseId: string;
  fileId: string | null;
  title: string;
  location: string;
  page: number | null;
  text: string;
}
export interface SearchHit {
  citation: Citation;
  score: number;
}
export interface SearchResult {
  items: SearchHit[];
  mode: string;
  warning: string | null;
}
export interface QuestionOption {
  id: string;
  text: string;
}
export interface Question extends Owned {
  knowledgeBaseId: string;
  type: string;
  stem: string;
  optionsJson: string;
  answersJson: string;
  explanation: string;
  citationsJson: string;
  tags: string;
  difficulty: string;
  status: string;
}
export interface QuestionInput {
  knowledgeBaseId: string;
  type: string;
  stem: string;
  options: QuestionOption[];
  answers: string[];
  explanation: string;
  citations: Citation[];
  tags: string;
  difficulty: string;
  revision?: number;
}
export interface Job extends Owned {
  kind: string;
  targetId: string;
  status: string;
  progress: number;
  attempts: number;
  error: string;
}
export interface AttemptQuestion {
  id: string;
  type: string;
  stem: string;
  options: QuestionOption[];
  selected: string[];
  answered: boolean;
  answers: string[] | null;
  explanation: string | null;
  citations: Citation[] | null;
  correct: boolean | null;
}
export interface Attempt {
  id: string;
  projectId: string;
  mode: string;
  status: string;
  revision: number;
  deadlineUtc: string | null;
  serverTimeUtc: string;
  score: number | null;
  passScore: number;
  questions: AttemptQuestion[];
}
export interface Mistake extends Owned {
  projectId: string;
  questionId: string;
  wrongCount: number;
  answerCount: number;
  mastered: boolean;
}
export interface Progress {
  attempts: {
    id: string;
    mode: string;
    status: string;
    score: number | null;
    createdAtUtc: string;
  }[];
  mistakes: Mistake[];
  answered: number;
  correct: number;
}
export interface History extends Owned {
  query: string;
  answer: string;
  citationsJson: string;
}
export interface StoredFile {
  id: string;
  name: string;
  contentType: string;
  length: number;
  sha256: string;
  purpose: string;
  ownerId: string;
  status: string;
  referenceCount: number;
  createdAtUtc: string;
}
export interface Location {
  id: string;
  name: string;
  rootPath: string;
  writable: boolean;
  availableBytes: number | null;
  fileCount: number;
  totalBytes: number;
}
export interface Account {
  id: string;
  userName: string;
  email: string;
  roles: string[];
}
export interface Role {
  id: string;
  name: string;
  permissions: string[];
}
const base = "/api/v1/exam-study";
const json = (body: unknown, method = "POST"): RequestInit => ({
  method,
  body: JSON.stringify(body),
});
export const studyApi = {
  bases: (all = false) =>
    request<KnowledgeBase[]>(`${base}/knowledge-bases?all=${all}`),
  saveBase: (
    body: {
      name: string;
      description: string;
      tags: string;
      revision?: number;
    },
    id?: string,
  ) =>
    request<KnowledgeBase>(
      `${base}/knowledge-bases${id ? `/${id}` : ""}`,
      json(body, id ? "PUT" : "POST"),
    ),
  deleteBase: (id: string) =>
    request(`${base}/knowledge-bases/${id}`, { method: "DELETE" }),
  sources: (id: string) =>
    request<Source[]>(`${base}/knowledge-bases/${id}/sources`),
  source: (id: string) => request<SourceDetail>(`${base}/sources/${id}`),
  saveSource: (
    kb: string,
    body: {
      title: string;
      fileId: string | null;
      text: string;
      revision?: number;
    },
    id?: string,
  ) =>
    request<Job>(
      `${base}/knowledge-bases/${kb}/sources${id ? `/${id}` : ""}`,
      json(body, id ? "PUT" : "POST"),
    ),
  reindex: (id: string) =>
    request<Job>(`${base}/sources/${id}/reindex`, { method: "POST" }),
  deleteSource: (id: string) =>
    request(`${base}/sources/${id}`, { method: "DELETE" }),
  projects: (all = false) => request<Project[]>(`${base}/projects?all=${all}`),
  saveProject: (
    body: {
      name: string;
      description: string;
      targetDate: string | null;
      knowledgeBaseIds: string[];
      revision?: number;
    },
    id?: string,
  ) =>
    request<Project>(
      `${base}/projects${id ? `/${id}` : ""}`,
      json(body, id ? "PUT" : "POST"),
    ),
  search: (
    id: string,
    body: {
      query: string;
      mode: string;
      knowledgeBaseId?: string;
      sourceId?: string;
      limit?: number;
      offset?: number;
    },
    signal?: AbortSignal,
  ) =>
    request<SearchResult>(`${base}/projects/${id}/search`, {
      ...json(body),
      signal,
    }),
  ask: (
    id: string,
    body: {
      query: string;
      mode: string;
      knowledgeBaseId?: string;
      sourceId?: string;
    },
    signal?: AbortSignal,
  ) =>
    request<History>(`${base}/projects/${id}/ask`, { ...json(body), signal }),
  history: (id: string) => request<History[]>(`${base}/projects/${id}/history`),
  questions: (kb?: string, all = false) =>
    request<Question[]>(
      `${base}/questions?all=${all}${kb ? `&knowledgeBaseId=${kb}` : ""}`,
    ),
  saveQuestion: (body: QuestionInput, id?: string) =>
    request<Question>(
      `${base}/questions${id ? `/${id}` : ""}`,
      json(body, id ? "PUT" : "POST"),
    ),
  questionStatus: (id: string, status: string) =>
    request<Question>(
      `${base}/questions/${id}/status`,
      json({ status }, "PUT"),
    ),
  generate: (body: {
    knowledgeBaseId: string;
    sourceId?: string;
    count: number;
    type: string;
    difficulty: string;
    tags: string;
  }) => request<Job>(`${base}/generation-jobs`, json(body)),
  startAttempt: (
    id: string,
    body: {
      mode: string;
      count: number;
      minutes: number;
      passScore: number;
      selection: string;
      types?: string[];
    },
  ) => request<Attempt>(`${base}/projects/${id}/attempts`, json(body)),
  attempt: (id: string) => request<Attempt>(`${base}/attempts/${id}`),
  answer: (id: string, q: string, answers: string[], revision: number) =>
    request<Attempt>(
      `${base}/attempts/${id}/answers/${q}`,
      json({ answers, revision }, "PUT"),
    ),
  submit: (id: string) =>
    request<Attempt>(`${base}/attempts/${id}/submit`, { method: "POST" }),
  progress: (id: string) =>
    request<Progress>(`${base}/projects/${id}/progress`),
  master: (id: string, mastered: boolean) =>
    request(`${base}/mistakes/${id}`, json({ mastered }, "PUT")),
  jobs: () => request<Job[]>(`${base}/jobs`),
  retry: (id: string) =>
    request(`${base}/jobs/${id}/retry`, { method: "POST" }),
  capabilities: () =>
    request<{
      chatConfigured: boolean;
      embeddingConfigured: boolean;
      qdrantAvailable: boolean;
    }>(`${base}/capabilities`),
};
export const fileApi = {
  async upload(file: File, purpose = "exam-study") {
    const body = new FormData();
    body.append("file", file);
    return request<StoredFile>(`/api/v1/files/?purpose=${purpose}`, {
      method: "POST",
      body,
    });
  },
  list: () => request<StoredFile[]>("/api/v1/files/"),
  references: (id: string) =>
    request<
      { id: string; module: string; entityId: string; ownerId: string }[]
    >(`/api/v1/files/${id}/references`),
  purge: (id: string) => request(`/api/v1/files/${id}`, { method: "DELETE" }),
  async download(id: string, name: string) {
    const response = await authorizedFetch(`/api/v1/files/${id}/content`);
    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.title ?? "下载失败");
    }
    const disposition = response.headers.get("Content-Disposition") ?? "";
    const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i);
    const plain = disposition.match(/filename="?([^";]+)/i);
    try {
      name = encoded ? decodeURIComponent(encoded[1]) : (plain?.[1] ?? name);
    } catch {
      /* Use supplied display name. */
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement("a");
    link.href = url;
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  },
  locations: () => request<Location[]>("/api/v1/storage/locations"),
  addLocation: (name: string, rootPath: string) =>
    request<Location>("/api/v1/storage/locations", json({ name, rootPath })),
  migrate: (sourceLocationId: string, targetLocationId: string) =>
    request<{ id: string }>(
      "/api/v1/storage/migrations",
      json({ sourceLocationId, targetLocationId }),
    ),
  cleanup: (id: string) =>
    request(`/api/v1/storage/migrations/${id}/cleanup`, { method: "POST" }),
};
export const identityApi = {
  users: () => request<Account[]>("/api/v1/identity/users"),
  roles: () => request<Role[]>("/api/v1/identity/roles"),
  create: (body: {
    userName: string;
    email: string;
    password: string;
    roleIds: string[];
  }) => request<string>("/api/v1/identity/users", json(body)),
  saveRole: (body: { name: string; permissions: string[] }, id?: string) =>
    request<string>(
      `/api/v1/identity/roles${id ? `/${id}` : ""}`,
      json(body, id ? "PUT" : "POST"),
    ),
  assign: (id: string, roleIds: string[]) =>
    request(`/api/v1/identity/users/${id}/roles`, json({ roleIds }, "PUT")),
  disable: (id: string) =>
    request(`/api/v1/identity/users/${id}`, { method: "DELETE" }),
  password: (currentPassword: string, newPassword: string) =>
    request("/api/v1/auth/password", json({ currentPassword, newPassword })),
};
