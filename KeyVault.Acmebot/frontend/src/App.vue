<script setup lang="ts">
import { ref } from 'vue'
import punycode from 'punycode'
import AddCertificate from './components/AddCertificate.vue'
import ShowCertificate from './components/ShowCertificate.vue'

const loading = ref(false)
const managedCertificates = ref([])
const unmanagedCertificates = ref([])

const openAdd = (): void => {
}

const refresh = (): void => {
}

const openDetails = (certificate: any): void => {
}

const toUnicode = (value: string): string => {
  return punycode.toUnicode(value);
}

const formatCreatedOn = (value: string): string => {
  return new Date(value).toLocaleString();
}

const formatExpiresOn = (value: string): string => {
  const date = Date.parse(value);
  const diff = date - Date.now();
  const remainDays = Math.round(diff / (1000 * 60 * 60 * 24));

  const remainText = diff > 0 ? `Expires in ${remainDays} days` : `EXPIRED`;

  return `${date.toLocaleString()} (${remainText})`;
}
</script>

<template>
  <h2 class="title is-4">
    Managed certificates
    <div class="buttons are-small is-inline-block is-pulled-right">
      <a class="button" @click="openAdd">
        <span class="icon">
          <i class="fas fa-plus"></i>
        </span>
        <span>Add</span>
      </a>
      <a class="button" @click="refresh" :class="{ 'is-loading': loading }">
        <span class="icon">
          <i class="fas fa-sync"></i>
        </span>
        <span>Refresh</span>
      </a>
    </div>
  </h2>
  <table class="table is-hoverable is-fullwidth">
    <thead>
      <tr>
        <th>Certificate name</th>
        <th>DNS Names</th>
        <th>Created On</th>
        <th>Expires On</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="certificate in managedCertificates"
        :class="{ 'has-background-danger-light': certificate.isExpired }"
      >
        <td>{{ certificate.name }}</td>
        <td>
          <div class="tags">
            <span
              v-for="dnsName in certificate.dnsNames"
              class="tag is-light is-medium"
            >{{ toUnicode(dnsName) }}</span>
          </div>
        </td>
        <td>{{ formatCreatedOn(certificate.createdOn) }}</td>
        <td>{{ formatExpiresOn(certificate.expiresOn) }}</td>
        <td>
          <div class="buttons are-small">
            <button class="button is-info" @click="openDetails(certificate)">Details</button>
          </div>
        </td>
      </tr>
    </tbody>
  </table>
  <div class="columns">
    <div class="column is-half">
      <h2 class="title is-4">Unmanaged certificates</h2>
      <table class="table is-hoverable is-fullwidth">
        <thead>
          <tr>
            <th>Certificate name</th>
            <th>DNS Names</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="certificate in unmanagedCertificates">
            <td>{{ certificate.name }}</td>
            <td>
              <div class="tags">
                <span
                  v-for="dnsName in certificate.dnsNames"
                  class="tag is-light is-medium"
                >{{ toUnicode(dnsName) }}</span>
              </div>
            </td>
            <td>
              <div class="buttons are-small">
                <button class="button is-info" @click="openDetails(certificate)">Details</button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>
