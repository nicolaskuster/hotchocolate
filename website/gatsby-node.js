const { createFilePath } = require("gatsby-source-filesystem");
const path = require("path");
const git = require("simple-git/promise");

exports.createPages = async ({ actions, graphql, reporter }) => {
  const { createPage, createRedirect } = actions;

  const result = await graphql(`
    {
      blog: allMdx(
        limit: 1000
        filter: { frontmatter: { path: { regex: "//blog(/.*)?/" } } }
        sort: { order: DESC, fields: [frontmatter___date] }
      ) {
        posts: nodes {
          fields {
            slug
          }
        }
        tags: group(field: frontmatter___tags) {
          fieldValue
        }
      }
      docs: allFile(
        limit: 1000
        filter: { sourceInstanceName: { eq: "docs" }, extension: { eq: "md" } }
      ) {
        pages: nodes {
          name
          relativeDirectory
          childMdx {
            fields {
              slug
            }
          }
        }
      }
      productsConfig: file(
        sourceInstanceName: { eq: "docs" }
        relativePath: { eq: "docs.json" }
      ) {
        products: childrenDocsJson {
          path
          latestStableVersion
        }
      }
    }
  `);

  // Handle errors
  if (result.errors) {
    reporter.panicOnBuild(`Error while running GraphQL query.`);
    return;
  }

  createBlogArticles(createPage, result.data.blog);

  const products = result.data.productsConfig.products;

  createDocPages(createPage, result.data.docs, products);

  createRedirect({
    fromPath: "/docs/",
    toPath: "/docs/hotchocolate/",
    redirectInBrowser: true,
    isPermanent: true,
  });

  const hotchocolate = products.find((p) => p.path === "hotchocolate");

  if (hotchocolate) {
    createHotChocolateRedirects(hotchocolate, createRedirect);
  }

  createRedirect({
    fromPath: "/docs/marshmallowpie/",
    toPath: "/docs/hotchocolate/",
    redirectInBrowser: true,
    isPermanent: true,
  });

  createRedirect({
    fromPath: "/blog/2019/03/18/entity-framework",
    toPath: "/blog/2020/03/18/entity-framework",
    redirectInBrowser: true,
    isPermanent: true,
  });

  // Banana Cake Pop
  createRedirect({
    fromPath: "/banana-cake-pop",
    toPath: "/products/bananacakepop",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/banana-cake-pop/",
    toPath: "/products/bananacakepop",
    redirectInBrowser: true,
    isPermanent: true,
  });

  // Products
  createRedirect({
    fromPath: "/products",
    toPath: "/",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/products/",
    toPath: "/",
    redirectInBrowser: true,
    isPermanent: true,
  });

  // company
  createRedirect({
    fromPath: "/company",
    toPath: "/",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/company/",
    toPath: "/",
    redirectInBrowser: true,
    isPermanent: true,
  });

  // images
  createRedirect({
    fromPath: "/img/projects/greendonut-banner.svg",
    toPath: "/resources/greendonut-banner.svg",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/greendonut-signet.png",
    toPath: "/resources/greendonut-signet.png",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/hotchocolate-banner.svg",
    toPath: "/resources/hotchocolate-banner.svg",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/hotchocolate-signet.png",
    toPath: "/resources/hotchocolate-signet.png",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/react-rasta-banner.svg",
    toPath: "/resources/react-rasta-banner.svg",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/react-rasta-signet.png",
    toPath: "/resources/react-rasta-signet.png",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/strawberryshake-banner.svg",
    toPath: "/resources/strawberryshake-banner.svg",
    redirectInBrowser: true,
    isPermanent: true,
  });
  createRedirect({
    fromPath: "/img/projects/strawberryshake-signet.png",
    toPath: "/resources/strawberryshake-signet.png",
    redirectInBrowser: true,
    isPermanent: true,
  });
};

exports.onCreateNode = async ({ node, actions, getNode, reporter }) => {
  const { createNodeField } = actions;

  if (node.internal.type !== `Mdx`) {
    return;
  }

  // if the path is defined on the frontmatter (like for blogs) use that as slug
  let path = node.frontmatter && node.frontmatter.path;

  if (!path) {
    path = createFilePath({ node, getNode });

    const parent = getNode(node.parent);

    // if the current file is emitted from the docs directory
    if (parent && parent.sourceInstanceName === "docs") {
      path = "/docs" + path;
    }

    // remove trailing slashes
    path = path.replace(/\/+$/, "");
  }

  createNodeField({
    name: `slug`,
    node,
    value: path,
  });

  let authorName = "Unknown";
  let lastUpdated = "0000-00-00";

  // we only run "git log" when building the production bundle
  // for development purposes we fallback to dummy values
  if (process.env.NODE_ENV === "production") {
    try {
      const result = await getGitLog(node.fileAbsolutePath);
      const data = result.latest || {};

      if (data.authorName) {
        authorName = data.authorName;
      }

      if (data.date) {
        lastUpdated = data.date;
      }
    } catch (error) {
      reporter.error(
        `Could not retrieve git information for ${node.fileAbsolutePath}`,
        error
      );
    }
  }

  createNodeField({
    node,
    name: `lastAuthorName`,
    value: authorName,
  });
  createNodeField({
    node,
    name: `lastUpdated`,
    value: lastUpdated,
  });
};

function createBlogArticles(createPage, data) {
  const blogArticleTemplate = path.resolve(
    `src/templates/blog-article-template.tsx`
  );
  const { posts, tags } = data;

  // Create Single Pages
  posts.forEach((post) => {
    createPage({
      path: post.fields.slug,
      component: blogArticleTemplate,
      context: {},
    });
  });

  // Create List Pages
  const postsPerPage = 20;
  const numPages = Math.ceil(posts.length / postsPerPage);

  Array.from({ length: numPages }).forEach((_, i) => {
    createPage({
      path: i === 0 ? `/blog` : `/blog/${i + 1}`,
      component: path.resolve("./src/templates/blog-articles-template.tsx"),
      context: {
        limit: postsPerPage,
        skip: i * postsPerPage,
        numPages,
        currentPage: i + 1,
      },
    });
  });

  // Create Tag Pages
  const tagTemplate = path.resolve(`src/templates/blog-tag-template.tsx`);

  tags.forEach((tag) => {
    createPage({
      path: `/blog/tags/${tag.fieldValue}`,
      component: tagTemplate,
      context: {
        tag: tag.fieldValue,
      },
    });
  });
}

function createDocPages(createPage, data, products) {
  const docTemplate = path.resolve(`src/templates/doc-page-template.tsx`);
  const { pages } = data;

  // Create Single Pages
  pages.forEach((page) => {
    const slug = page.childMdx.fields.slug;
    const originPath = `${page.relativeDirectory}/${page.name}.md`;

    const product = getProductFromSlug(slug, products);

    if (product && product.version === product.latestStableVersion) {
      const unversionedSlug = slug.replace(
        product.basePath,
        "/docs/" + product.path
      );

      // Instead of duplicating the page here, we could just create a page that
      // does a JS redirect to the actual (versioned) slug. Google's crawler
      // should handle that just fine. But just to be fully backwards compatible,
      // this duplicates all of the versioned pages of the latest
      // stable version as unversioned pages.
      // If v12 is the stable version, two versions will live side by side:
      // /docs/hotchocolate/v12/whatever
      // /docs/hotchocolate/whatever

      createPage({
        path: unversionedSlug,
        component: docTemplate,
        context: {
          originPath,
        },
      });
    }

    createPage({
      path: slug,
      component: docTemplate,
      context: {
        originPath,
      },
    });
  });
}

function createHotChocolateRedirects(product, createRedirect) {
  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/code-first`,
    toPath: "/docs/hotchocolate/defining-a-schema",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema-first`,
    toPath: "/docs/hotchocolate/defining-a-schema",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema`,
    toPath: "/docs/hotchocolate/defining-a-schema",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/object-type`,
    toPath: "/docs/hotchocolate/defining-a-schema/object-types",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/interface-type`,
    toPath: "/docs/hotchocolate/defining-a-schema/interfaces",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/union-type`,
    toPath: "/docs/hotchocolate/defining-a-schema/unions",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/input-object-type`,
    toPath: "/docs/hotchocolate/defining-a-schema/input-object-types",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/enum-type`,
    toPath: "/docs/hotchocolate/defining-a-schema/enums",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/descriptions`,
    toPath: "/docs/hotchocolate/defining-a-schema/documentation",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/custom-scalar-types`,
    toPath: "/docs/hotchocolate/defining-a-schema/scalars",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/directives`,
    toPath: "/docs/hotchocolate/defining-a-schema/directives",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/relay`,
    toPath: "/docs/hotchocolate/defining-a-schema/relay",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/schema/resolvers`,
    toPath: "/docs/hotchocolate/fetching-data/resolvers",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/data-fetching`,
    toPath: "/docs/hotchocolate/fetching-data/dataloader",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/data-fetching/pagination`,
    toPath: "/docs/hotchocolate/fetching-data/pagination",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/data-fetching/filters`,
    toPath: "/docs/hotchocolate/fetching-data/filtering",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/instrumentation`,
    toPath: "/docs/hotchocolate/server/instrumentation",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/apollo-tracing`,
    toPath: "/docs/hotchocolate/server/instrumentation#apollo-tracing",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/persisted-queries`,
    toPath: "/docs/hotchocolate/performance/persisted-queries",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/custom-context-data`,
    toPath: "/docs/hotchocolate/execution-engine",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/subscriptions`,
    toPath: "/docs/hotchocolate/defining-a-schema/subscriptions",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/type-conversion`,
    toPath: "/docs/hotchocolate/defining-a-schema/scalars#custom-converters",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/execution-engine/type-conversion`,
    toPath: "/docs/hotchocolate/defining-a-schema/scalars#custom-converters",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/security/security`,
    toPath: "/docs/hotchocolate/security",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/stitching`,
    toPath: "/docs/hotchocolate/distributed-schema",
    redirectInBrowser: true,
  });

  createRedirect({
    fromPath: `/docs/hotchocolate/${product.latestStableVersion}/tutorials`,
    toPath: "/docs/hotchocolate/integrations/entity-framework",
    redirectInBrowser: true,
  });
}

const productAndVersionPattern = /^\/docs\/([\w-]+)(?:\/(v\d+))?/;

function getProductFromSlug(slug, products) {
  const productMatch = productAndVersionPattern.exec(slug);

  if (!productMatch) {
    return null;
  }

  const productName = productMatch[1] || "";
  const productVersion = productMatch[2] || "";

  const product = products.find((p) => p?.path === productName);

  return {
    ...product,
    version: productVersion,
    basePath: productMatch[0],
  };
}

function getGitLog(filepath) {
  const logOptions = {
    file: filepath,
    n: 1,
    format: {
      date: `%cs`,
      authorName: `%an`,
    },
  };

  return git().log(logOptions);
}
